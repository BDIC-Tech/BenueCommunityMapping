using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Geography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope   = services.CreateScope();
            var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var log     = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            await db.Database.MigrateAsync();

            // ── Roles ──────────────────────────────────────────────────
            foreach (var role in AppRoles.All)
                if (!await roleMgr.RoleExistsAsync(role))
                    await roleMgr.CreateAsync(new IdentityRole(role));

            // ── Geographic seed data (Benue State sample) ──────────────
            if (!await db.LGAs.AnyAsync())
            {
                var lgas = new[]
                {
                    new LocalGovernmentArea { Name = "Makurdi",    Code = "BN-MKD" },
                    new LocalGovernmentArea { Name = "Gboko",      Code = "BN-GBK" },
                    new LocalGovernmentArea { Name = "Otukpo",     Code = "BN-OTK" },
                    new LocalGovernmentArea { Name = "Katsina-Ala",Code = "BN-KTA" },
                    new LocalGovernmentArea { Name = "Ado",        Code = "BN-ADO" },
                    new LocalGovernmentArea { Name = "Agatu",      Code = "BN-AGT" },
                    new LocalGovernmentArea { Name = "Apa",        Code = "BN-APA" },
                    new LocalGovernmentArea { Name = "Buruku",     Code = "BN-BRK" },
                    new LocalGovernmentArea { Name = "Guma",       Code = "BN-GMA" },
                    new LocalGovernmentArea { Name = "Gwer East",  Code = "BN-GWE" },
                    new LocalGovernmentArea { Name = "Gwer West",  Code = "BN-GWW" },
                    new LocalGovernmentArea { Name = "Konshisha",  Code = "BN-KSH" },
                    new LocalGovernmentArea { Name = "Kwande",     Code = "BN-KWD" },
                    new LocalGovernmentArea { Name = "Logo",       Code = "BN-LGO" },
                    new LocalGovernmentArea { Name = "Obi",        Code = "BN-OBI" },
                    new LocalGovernmentArea { Name = "Ogbadibo",   Code = "BN-OGB" },
                    new LocalGovernmentArea { Name = "Ohimini",    Code = "BN-OHM" },
                    new LocalGovernmentArea { Name = "Oju",        Code = "BN-OJU" },
                    new LocalGovernmentArea { Name = "Okpokwu",    Code = "BN-OKP" },
                    new LocalGovernmentArea { Name = "Tarka",      Code = "BN-TRK" },
                    new LocalGovernmentArea { Name = "Ukum",       Code = "BN-UKM" },
                    new LocalGovernmentArea { Name = "Ushongo",    Code = "BN-USH" },
                    new LocalGovernmentArea { Name = "Vandeikya",  Code = "BN-VDK" },
                };
                db.LGAs.AddRange(lgas);
                await db.SaveChangesAsync();

                // Sample wards for Makurdi
                var makurdi = lgas.First(l => l.Name == "Makurdi");
                var wards = new[]
                {
                    new Ward { Name = "North Bank",    Code = "MKD-W01", LocalGovernmentAreaId = makurdi.Id },
                    new Ward { Name = "South Bank",    Code = "MKD-W02", LocalGovernmentAreaId = makurdi.Id },
                    new Ward { Name = "Wadata",        Code = "MKD-W03", LocalGovernmentAreaId = makurdi.Id },
                    new Ward { Name = "Clerk Quarters",Code = "MKD-W04", LocalGovernmentAreaId = makurdi.Id },
                };
                db.Wards.AddRange(wards);
                await db.SaveChangesAsync();

                // Sample kindreds for North Bank ward
                var northBank = wards.First(w => w.Name == "North Bank");
                var kindreds  = new[]
                {
                    new Kindred { Name = "Abagena",  Code = "NB-K01", WardId = northBank.Id },
                    new Kindred { Name = "Achusa",   Code = "NB-K02", WardId = northBank.Id },
                    new Kindred { Name = "Jukun",    Code = "NB-K03", WardId = northBank.Id },
                };
                db.Kindreds.AddRange(kindreds);
                await db.SaveChangesAsync();

                // Sample communities for Abagena kindred
                var abagena = kindreds.First(k => k.Name == "Abagena");
                var communities = new[]
                {
                    new Community { Name = "Abagena Central", Code = "ABG-C01", KindredId = abagena.Id,
                        MajorEthnicGroups = "Tiv", EstimatedPopulation = 1200 },
                    new Community { Name = "Abagena North",   Code = "ABG-C02", KindredId = abagena.Id,
                        MajorEthnicGroups = "Tiv", EstimatedPopulation = 850 },
                };
                db.Communities.AddRange(communities);
                await db.SaveChangesAsync();
            }

            // ── Default Admin ──────────────────────────────────────────
            const string adminEmail = "bsbs@benuestate.gov.ng";
            if (await userMgr.FindByEmailAsync(adminEmail) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail, Email = adminEmail,
                    FirstName = "System", LastName = "Administrator",
                    IsActive = true, EmailConfirmed = true
                };
                var res = await userMgr.CreateAsync(admin, "Admin@12345!");
                if (res.Succeeded) await userMgr.AddToRoleAsync(admin, AppRoles.Admin);
                else log.LogError("Seed admin failed: {e}",
                    string.Join(", ", res.Errors.Select(x => x.Description)));
            }

            // ── Sample Coordinator ─────────────────────────────────────
            const string coordEmail = "coordinator@benuestate.gov.ng";
            if (await userMgr.FindByEmailAsync(coordEmail) is null)
            {
                var coord = new ApplicationUser
                {
                    UserName = coordEmail,
                    Email = coordEmail,
                    FirstName = "D-Best", 
                    LastName = "Coordinator",
                    LocalGovernmentArea = "Makurdi",
                    IsActive = true, EmailConfirmed = true
                };
                var res = await userMgr.CreateAsync(coord, "Coord@12345!");
                if (res.Succeeded) await userMgr.AddToRoleAsync(coord, AppRoles.Coordinator);
            }

            // ── Sample Agent ───────────────────────────────────────────
            var coordUser = await userMgr.FindByEmailAsync(coordEmail);
            const string agentEmail = "agent@benuestate.gov.ng";
            if (await userMgr.FindByEmailAsync(agentEmail) is null && coordUser is not null)
            {
                var agent = new ApplicationUser
                {
                    UserName = agentEmail, Email = agentEmail,
                    FirstName = "Samson", LastName = "Agent",
                    LocalGovernmentArea = "Makurdi", AssignedWard = "North Bank",
                    CoordinatorId = coordUser.Id,
                    IsActive = true, EmailConfirmed = true
                };
                var res = await userMgr.CreateAsync(agent, "Agent@12345!");
                if (res.Succeeded) await userMgr.AddToRoleAsync(agent, AppRoles.Agent);
            }
        }
    }
}
