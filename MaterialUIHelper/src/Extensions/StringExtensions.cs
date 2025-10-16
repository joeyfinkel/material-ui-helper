namespace MaterialUIHelper.Extensions;

public static class StringExtensions
{
    public static string ToUpperFirstLetter(this string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return string.Empty;
        }
        
        return char.ToUpper(str[0]) + str[1..];
    }
}