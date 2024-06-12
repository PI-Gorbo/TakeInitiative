using System.Reflection;
using System.Text;

public static class EmbeddedResource
{
    public static string TestTxt
    {
        get
        {
            var info = Assembly.GetExecutingAssembly().GetName();
            var name = info.Name;
            using var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"{name}.Embedded.ConfirmationEmail.mjml")!;
            using var streamReader = new StreamReader(stream, Encoding.UTF8);
            return streamReader.ReadToEnd();
        }
    }
}