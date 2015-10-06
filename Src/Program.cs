using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MicroJpegStrip
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("MicroJpegStrip v001");
            Console.WriteLine();
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: MicroJpegStrip.exe [-o] jpeg1 [jpeg2 [...]]");
                Console.WriteLine();
                Console.WriteLine("Strips all non-image data from a JPEG file. This includes any ICC color profiles as well as all other metadata.");
                Console.WriteLine("By default, this program will create a new file for each input file, adding '.stripped' before the extension.");
                Console.WriteLine("If the destination file already exists, a number will be appended to the output file name to make it unique.");
                Console.WriteLine();
                Console.WriteLine("Specify -o (overwrite) as the first parameter to overwrite each input file instead.");
                Console.WriteLine();
                Console.WriteLine("Returns 0 if all files were processed successfully, otherwise returns the number of files with errors.");
                return 0;
            }
            bool overwrite = args[0] == "-o";
            if (overwrite)
                args = args.Skip(1).ToArray();

            int errors = 0;
            foreach (var arg in args)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(arg);
                Console.ResetColor();
                Console.Write(": ");
                errors++;
                try
                {
                    var bytes = File.ReadAllBytes(arg);
                    if (bytes.Length < 2 || bytes[0] != 0xFF || bytes[1] != 0xD8 /* start of image */)
                        throw new Exception("not a JPEG file");
                    int inp = 2;
                    int outp = 2;
                    bool wantedMarker = false;
                    while (inp < bytes.Length)
                    {
                        if (bytes[inp] == 0xFF)
                        {
                            if (inp == bytes.Length - 1)
                                throw new Exception("unexpected end of JPEG file");
                            byte next = bytes[inp + 1];
                            if (next == 0xD9 /* end of image */)
                                break;
                            if (next == 0xC0 /* baseline dct */ || next == 0xC2 /* progressive dct */ || next == 0xC4 /* huffman table */ || next == 0xDB /* quantization table */ || next == 0xDA /* scan data */)
                                wantedMarker = true;
#if PRESERVE_RESTARTS
                            else if (next == 0xDD /* restart interval */ || ((next & 0xD8) == 0xD8) /* restart */ )
                                wantedMarker = true;
#endif
                            else if (next != 0) // 0xFF 0x00 is an escape sequence for 0xFF and not a marker
                                wantedMarker = false;
                        }
                        if (wantedMarker)
                            bytes[outp++] = bytes[inp];
                        inp++;
                    }
                    string filename;
                    if (overwrite)
                        filename = arg;
                    else
                        for (int num = 0; ; num++)
                        {
                            var suffix = ".stripped" + (num == 0 ? "" : num.ToString());
                            filename = Regex.Replace(arg, @"(?=\.[^.]+$)", suffix);
                            if (filename == arg)
                                filename += suffix;
                            if (!File.Exists(filename))
                                break;
                        }
                    using (var f = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
#if NO_JFIF
                        f.Write(bytes, 0, outp);
#else
                        f.Write(bytes, 0, 2);
                        var app0 = new byte[] { /* app0 marker */ 0xFF, 0xE0, /* length */ 0, 16, /* "JFIF\0" */ 0x4A, 0x46, 0x49, 0x46, 0, /* v1.2 */ 1, 2, /* dpi units */ 1, /* dpiX & Y */ 0, 1, 0, 1, /* thumbX & Y */ 0, 0 };
                        f.Write(app0, 0, app0.Length);
                        f.Write(bytes, 2, outp - 2);
#endif
                        f.Write(new byte[] { 0xFF, 0xD9 }, 0, 2);
                    }
                    Console.WriteLine("success");
                    errors--;
                }
                catch (FileNotFoundException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("file not found");
                    Console.ResetColor();
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                }
            }

            return errors;
        }
    }
}
