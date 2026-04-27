using System.Text;
using BenueCommunityMapping.Services.Analytics;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace BenueCommunityMapping.Services.Export
{
    public class ExportService : IExportService
    {
        private readonly IAnalyticsService _analytics;

        public ExportService(IAnalyticsService analytics)
        {
            _analytics = analytics;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public IReadOnlyList<(string Key, string Label)> AvailableSections { get; } =
        [
            ("all",          "All Sections (Summary)"),
            ("demographics", "Section A – Demographics & IDPs"),
            ("markets",      "Section B – Markets"),
            ("health",       "Section C – Health"),
            ("education",    "Section D – Education"),
            ("roads",        "Section E – Roads"),
            ("finance",      "Section F – Finance"),
            ("environment",  "Section G – Environment"),
            ("telecom",      "Section I – Telecom"),
            ("security",     "Section J – Security"),
            ("geo_lga",      "Geographic – LGA Summary"),
            ("community",    "Community-Level Metrics"),
        ];

        // ── Helpers ──────────────────────────────────────────────────────

        private async Task<List<(string Label, int Count, double Pct, double Ratio)>> GetSectionRows(
            string section, AnalyticsFilter filter)
        {
            var dash = await _analytics.GetNumericalDashboardAsync(filter);
            if (dash is null) return [];

            var metrics = section switch
            {
                "health"       => dash.HealthMetrics,
                "education"    => dash.EducationMetrics,
                "markets"      => dash.MarketMetrics,
                "roads"        => dash.RoadMetrics,
                "finance"      => dash.FinanceMetrics,
                "environment"  => dash.EnvironmentMetrics,
                "security"     => dash.SecurityMetrics,
                "telecom"      => dash.TelecomMetrics,
                _ => CombineAll(dash),
            };

            return metrics.Select(m => (m.Label, m.Count, m.Percentage, m.RatioPer1000HH)).ToList();
        }

        private static IReadOnlyList<MetricRow> CombineAll(NumericalDashboard d)
        {
            var all = new List<MetricRow>();
            all.AddRange(d.HealthMetrics);
            all.AddRange(d.EducationMetrics);
            all.AddRange(d.MarketMetrics);
            all.AddRange(d.RoadMetrics);
            all.AddRange(d.FinanceMetrics);
            all.AddRange(d.EnvironmentMetrics);
            all.AddRange(d.SecurityMetrics);
            all.AddRange(d.TelecomMetrics);
            return all;
        }

        private string GetSectionLabel(string section) =>
            AvailableSections.FirstOrDefault(s => s.Key == section).Label ?? section;

        // ═════════════════════════════════════════════════════════════════
        //  EXCEL
        // ═════════════════════════════════════════════════════════════════

        public async Task<byte[]> ExportExcelAsync(string section, AnalyticsFilter filter)
        {
            using var wb = new XLWorkbook();
            var title = GetSectionLabel(section);

            if (section == "geo_lga")
            {
                await AddGeoLgaSheet(wb, filter);
            }
            else if (section == "community")
            {
                await AddCommunitySheet(wb, filter);
            }
            else
            {
                var rows = await GetSectionRows(section, filter);
                var ws = wb.AddWorksheet(title.Length > 31 ? title[..31] : title);
                ws.Cell(1, 1).Value = "Metric";
                ws.Cell(1, 2).Value = "Count";
                ws.Cell(1, 3).Value = "Percentage (%)";
                ws.Cell(1, 4).Value = "Per 1,000 HH";
                StyleHeader(ws, 4);
                for (int r = 0; r < rows.Count; r++)
                {
                    ws.Cell(r + 2, 1).Value = rows[r].Label;
                    ws.Cell(r + 2, 2).Value = rows[r].Count;
                    ws.Cell(r + 2, 3).Value = Math.Round(rows[r].Pct, 2);
                    ws.Cell(r + 2, 4).Value = Math.Round(rows[r].Ratio, 2);
                }
                ws.Columns().AdjustToContents();
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private async Task AddGeoLgaSheet(XLWorkbook wb, AnalyticsFilter filter)
        {
            var rows = await _analytics.GetLGASummaryAsync(filter);
            var ws = wb.AddWorksheet("LGA Summary");
            ws.Cell(1, 1).Value = "LGA";
            ws.Cell(1, 2).Value = "Code";
            ws.Cell(1, 3).Value = "Total Submissions";
            ws.Cell(1, 4).Value = "Approved";
            ws.Cell(1, 5).Value = "Coverage Rate (%)";
            StyleHeader(ws, 5);
            for (int r = 0; r < rows.Count; r++)
            {
                ws.Cell(r + 2, 1).Value = rows[r].Name;
                ws.Cell(r + 2, 2).Value = rows[r].Code;
                ws.Cell(r + 2, 3).Value = rows[r].TotalSubmissions;
                ws.Cell(r + 2, 4).Value = rows[r].ApprovedSubmissions;
                ws.Cell(r + 2, 5).Value = Math.Round(rows[r].CoverageRate, 2);
            }
            ws.Columns().AdjustToContents();
        }

        private async Task AddCommunitySheet(XLWorkbook wb, AnalyticsFilter filter)
        {
            var rows = await _analytics.GetCommunityMetricsAsync(filter);
            var ws = wb.AddWorksheet("Community Metrics");
            string[] hdrs = ["Community","Kindred","Ward","LGA","Households","Health Fac.",
                "Schools","Children OOS","Markets","Ambulance","Tarred Road",
                "Formal Banking","Borehole","Security","Farmer-Herder","Priority Need"];
            for (int c = 0; c < hdrs.Length; c++) ws.Cell(1, c + 1).Value = hdrs[c];
            StyleHeader(ws, hdrs.Length);
            for (int r = 0; r < rows.Count; r++)
            {
                var m = rows[r];
                ws.Cell(r + 2, 1).Value  = m.CommunityName;
                ws.Cell(r + 2, 2).Value  = m.KindredName;
                ws.Cell(r + 2, 3).Value  = m.WardName;
                ws.Cell(r + 2, 4).Value  = m.LGAName;
                ws.Cell(r + 2, 5).Value  = m.EstHouseholds;
                ws.Cell(r + 2, 6).Value  = m.HealthFacilities;
                ws.Cell(r + 2, 7).Value  = m.Schools;
                ws.Cell(r + 2, 8).Value  = m.ChildrenNotInSchool;
                ws.Cell(r + 2, 9).Value  = m.Markets;
                ws.Cell(r + 2, 10).Value = m.FunctionalAmbulance ? "Yes" : "No";
                ws.Cell(r + 2, 11).Value = m.TarredRoad ? "Yes" : "No";
                ws.Cell(r + 2, 12).Value = m.FormalBanking ? "Yes" : "No";
                ws.Cell(r + 2, 13).Value = m.Borehole ? "Yes" : "No";
                ws.Cell(r + 2, 14).Value = m.SecuritySituation ?? "–";
                ws.Cell(r + 2, 15).Value = m.FarmerHerderConflict ? "Yes" : "No";
                ws.Cell(r + 2, 16).Value = m.TopPriorityNeed ?? "–";
            }
            ws.Columns().AdjustToContents();
        }

        private static void StyleHeader(IXLWorksheet ws, int cols)
        {
            var hdr = ws.Range(1, 1, 1, cols);
            hdr.Style.Font.Bold = true;
            hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E7D32");
            hdr.Style.Font.FontColor = XLColor.White;
            hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // ═════════════════════════════════════════════════════════════════
        //  CSV
        // ═════════════════════════════════════════════════════════════════

        public async Task<byte[]> ExportCsvAsync(string section, AnalyticsFilter filter)
        {
            var sb = new StringBuilder();

            if (section == "geo_lga")
            {
                var rows = await _analytics.GetLGASummaryAsync(filter);
                sb.AppendLine("LGA,Code,Total Submissions,Approved,Coverage Rate (%)");
                foreach (var r in rows)
                    sb.AppendLine($"\"{r.Name}\",\"{r.Code}\",{r.TotalSubmissions},{r.ApprovedSubmissions},{r.CoverageRate:F2}");
            }
            else if (section == "community")
            {
                var rows = await _analytics.GetCommunityMetricsAsync(filter);
                sb.AppendLine("Community,Kindred,Ward,LGA,Households,Health Fac.,Schools,Children OOS,Markets,Ambulance,Tarred Road,Formal Banking,Borehole,Security,Farmer-Herder,Priority Need");
                foreach (var m in rows)
                    sb.AppendLine($"\"{m.CommunityName}\",\"{m.KindredName}\",\"{m.WardName}\",\"{m.LGAName}\",{m.EstHouseholds},{m.HealthFacilities},{m.Schools},{m.ChildrenNotInSchool},{m.Markets},{(m.FunctionalAmbulance ? "Yes" : "No")},{(m.TarredRoad ? "Yes" : "No")},{(m.FormalBanking ? "Yes" : "No")},{(m.Borehole ? "Yes" : "No")},\"{m.SecuritySituation ?? "–"}\",{(m.FarmerHerderConflict ? "Yes" : "No")},\"{m.TopPriorityNeed ?? "–"}\"");
            }
            else
            {
                var rows = await GetSectionRows(section, filter);
                sb.AppendLine("Metric,Count,Percentage (%),Per 1000 HH");
                foreach (var r in rows)
                    sb.AppendLine($"\"{r.Label}\",{r.Count},{r.Pct:F2},{r.Ratio:F2}");
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PDF
        // ═════════════════════════════════════════════════════════════════

        public async Task<byte[]> ExportPdfAsync(string section, AnalyticsFilter filter)
        {
            var title = GetSectionLabel(section);

            if (section == "geo_lga")
                return await BuildGeoPdf(filter, title);
            if (section == "community")
                return await BuildCommunityPdf(filter, title);

            var rows = await GetSectionRows(section, filter);
            return BuildMetricPdf(rows, title);
        }

        private byte[] BuildMetricPdf(List<(string Label, int Count, double Pct, double Ratio)> rows, string title)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.Header().Text(title).FontSize(16).Bold().FontColor(Colors.Green.Darken3);
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Metric").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Count").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("% ").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Per 1K HH").FontColor(Colors.White).Bold();
                        });
                        foreach (var r in rows)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.Label);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.Count.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{r.Pct:F1}%");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{r.Ratio:F2}");
                        }
                    });
                    page.Footer().AlignCenter().Text(t => { t.Span("Benue Community Mapping — Generated "); t.Span(DateTime.Now.ToString("dd MMM yyyy HH:mm")); });
                });
            });
            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            return ms.ToArray();
        }

        private async Task<byte[]> BuildGeoPdf(AnalyticsFilter filter, string title)
        {
            var rows = await _analytics.GetLGASummaryAsync(filter);
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.Header().Text(title).FontSize(16).Bold().FontColor(Colors.Green.Darken3);
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("LGA").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Code").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Total").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Approved").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Coverage %").FontColor(Colors.White).Bold();
                        });
                        foreach (var r in rows)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.Name);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.Code);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.TotalSubmissions.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.ApprovedSubmissions.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{r.CoverageRate:F1}%");
                        }
                    });
                    page.Footer().AlignCenter().Text(t => { t.Span("Benue Community Mapping — Generated "); t.Span(DateTime.Now.ToString("dd MMM yyyy HH:mm")); });
                });
            });
            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            return ms.ToArray();
        }

        private async Task<byte[]> BuildCommunityPdf(AnalyticsFilter filter, string title)
        {
            var rows = await _analytics.GetCommunityMetricsAsync(filter);
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.Header().Text(title).FontSize(14).Bold().FontColor(Colors.Green.Darken3);
                    page.Content().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); c.RelativeColumn(1);
                            c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            foreach (var hdr in new[] { "Community", "Ward", "LGA", "HH", "Health", "Schools", "OOS", "Markets" })
                                h.Cell().Background(Colors.Green.Darken3).Padding(3).Text(hdr).FontSize(8).FontColor(Colors.White).Bold();
                        });
                        foreach (var m in rows)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.CommunityName).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.WardName).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.LGAName).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.EstHouseholds.ToString()).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.HealthFacilities.ToString()).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.Schools.ToString()).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.ChildrenNotInSchool.ToString()).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.Markets.ToString()).FontSize(8);
                        }
                    });
                    page.Footer().AlignCenter().Text(t => { t.Span("Benue Community Mapping — Generated "); t.Span(DateTime.Now.ToString("dd MMM yyyy HH:mm")); });
                });
            });
            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            return ms.ToArray();
        }

        // ═════════════════════════════════════════════════════════════════
        //  DOCX (OpenXML)
        // ═════════════════════════════════════════════════════════════════

        public async Task<byte[]> ExportDocxAsync(string section, AnalyticsFilter filter)
        {
            var title = GetSectionLabel(section);
            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document();
                var body = mainPart.Document.AppendChild(new W.Body());

                // Title
                body.AppendChild(new W.Paragraph(
                    new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }),
                    new W.Run(new W.RunProperties(new W.Bold(), new W.FontSize { Val = "32" }, new W.Color { Val = "2E7D32" }),
                        new W.Text(title))));
                body.AppendChild(new W.Paragraph(
                    new W.Run(new W.RunProperties(new W.Italic(), new W.FontSize { Val = "20" }),
                        new W.Text($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}"))));

                if (section == "geo_lga")
                    await AddGeoTableToDoc(body, filter);
                else if (section == "community")
                    await AddCommunityTableToDoc(body, filter);
                else
                    await AddMetricTableToDoc(body, section, filter);

                mainPart.Document.Save();
            }
            return ms.ToArray();
        }

        private async Task AddMetricTableToDoc(W.Body body, string section, AnalyticsFilter filter)
        {
            var rows = await GetSectionRows(section, filter);
            var table = new W.Table();
            table.AppendChild(DocxTableProps());
            table.AppendChild(DocxHeaderRow("Metric", "Count", "Percentage (%)", "Per 1,000 HH"));
            foreach (var r in rows)
                table.AppendChild(DocxDataRow(r.Label, r.Count.ToString(), $"{r.Pct:F1}", $"{r.Ratio:F2}"));
            body.AppendChild(table);
        }

        private async Task AddGeoTableToDoc(W.Body body, AnalyticsFilter filter)
        {
            var rows = await _analytics.GetLGASummaryAsync(filter);
            var table = new W.Table();
            table.AppendChild(DocxTableProps());
            table.AppendChild(DocxHeaderRow("LGA", "Code", "Total", "Approved", "Coverage %"));
            foreach (var r in rows)
                table.AppendChild(DocxDataRow(r.Name, r.Code, r.TotalSubmissions.ToString(),
                    r.ApprovedSubmissions.ToString(), $"{r.CoverageRate:F1}%"));
            body.AppendChild(table);
        }

        private async Task AddCommunityTableToDoc(W.Body body, AnalyticsFilter filter)
        {
            var rows = await _analytics.GetCommunityMetricsAsync(filter);
            var table = new W.Table();
            table.AppendChild(DocxTableProps());
            table.AppendChild(DocxHeaderRow("Community", "Ward", "LGA", "HH", "Health", "Schools", "OOS", "Markets"));
            foreach (var m in rows)
                table.AppendChild(DocxDataRow(m.CommunityName, m.WardName, m.LGAName,
                    m.EstHouseholds.ToString(), m.HealthFacilities.ToString(),
                    m.Schools.ToString(), m.ChildrenNotInSchool.ToString(), m.Markets.ToString()));
            body.AppendChild(table);
        }

        private static W.TableProperties DocxTableProps() => new(
            new W.TableBorders(
                new W.TopBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 }),
            new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" });

        private static W.TableRow DocxHeaderRow(params string[] cells)
        {
            var row = new W.TableRow();
            foreach (var c in cells)
                row.AppendChild(new W.TableCell(
                    new W.TableCellProperties(new W.Shading { Fill = "2E7D32", Val = W.ShadingPatternValues.Clear }),
                    new W.Paragraph(new W.Run(
                        new W.RunProperties(new W.Bold(), new W.Color { Val = "FFFFFF" }, new W.FontSize { Val = "20" }),
                        new W.Text(c)))));
            return row;
        }

        private static W.TableRow DocxDataRow(params string[] cells)
        {
            var row = new W.TableRow();
            foreach (var c in cells)
                row.AppendChild(new W.TableCell(
                    new W.Paragraph(new W.Run(
                        new W.RunProperties(new W.FontSize { Val = "20" }),
                        new W.Text(c)))));
            return row;
        }
    }
}
