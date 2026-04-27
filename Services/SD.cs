namespace BenueCommunityMapping.Services
{
    public static class SD
    {
        public static int ToIndex(int number)
        {
            var index = number - 1;
            return index < 0 ? 0 : index;
        }
    }
}
