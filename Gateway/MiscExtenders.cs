using MimeKit;
using SendGrid.Helpers.Mail;

namespace Gateway;

public static class MiscExtenders
{
    public static R AsFunc<T, R>(
        this T value, Func<T, R> func) => func(value);

    public static string StringOrNull(this string value) =>
        string.IsNullOrWhiteSpace(value) ? null! : value;

    public static EmailAddress ToEmailAddress(this InternetAddress address) =>
        new(address.ToString(), address.Name.StringOrNull());
}