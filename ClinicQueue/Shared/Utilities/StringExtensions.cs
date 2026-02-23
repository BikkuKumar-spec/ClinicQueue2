using System.Globalization;

namespace ClinicQueue.Shared.Utilities
{
    public static class StringExtensions
    {
        public static string ToProperCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var textInfo = CultureInfo.CurrentCulture.TextInfo;
            // First convert to lowercase to handle cases like "yash thAkur"
            return textInfo.ToTitleCase(input.ToLower());
        }
    }
}
