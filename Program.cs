using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace String2JSON
{
    internal class Program
    {
        static void Main(string[] args)
        {
            StringFile stringFile, newstringFile;
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            if (args.Length>0)
            {
                if (args[0].Length > 4)
                {
                    var tale= args[0].Substring(args[0].Length - 4);
                    if (tale == "ring")
                    {
                        string jsonString;
                        Console.WriteLine("Converting .string="+args[0]+"\n to .json="+args[0]+".json");
                        FileStream fs = File.OpenRead(args[0]);
                        stringFile = new();
                        stringFile.GetStringFile(fs);
                        jsonString = JsonSerializer.Serialize(stringFile, typeof(StringFile), options);
                        File.WriteAllText(args[0]+".json", jsonString);
                    }
                    if (tale =="json")
                    {
                        string jsonString;
                        Console.WriteLine("Converting .json=" + args[0] + "\n to .string=" + args[0] + ".string");
                        FileStream fs = File.OpenRead(args[0]);
                        jsonString=File.ReadAllText(args[0]);
                        newstringFile = JsonSerializer.Deserialize<StringFile>(jsonString, options);
                        using (FileStream newfs = new FileStream(args[0] + ".string", FileMode.Create))
                        {
                            newstringFile.WriteStringFile(newfs);
                        }
                    }
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
