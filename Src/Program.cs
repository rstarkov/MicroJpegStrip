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
                    while (true)
                    {
                        if (inp >= bytes.Length - 1) // we're expecting a marker, which is 2 bytes long ...
                            throw new Exception("unexpected end of JPEG file");
                        if (bytes[inp] != 0xFF) // ... and starts with FF
                            throw new Exception("invalid or unsupported JPEG file");

                        byte next = bytes[inp + 1];

                        if (next == 0xD9 /* end of image */)
                            break;
                        else if (next == 0xC0 /* baseline dct */ || next == 0xC2 /* progressive dct */ || next == 0xC4 /* huffman table */ || next == 0xDB /* quantization table */
                            || next == 0xDD /* restart interval */ || next == 0xDA /* scan data */)
                        {
                            int length = 2 + (bytes[inp + 2] << 8) + bytes[inp + 3];
                            Buffer.BlockCopy(bytes, inp, bytes, outp, length);
                            inp += length;
                            outp += length;

                            if (next == 0xDA)
                            {
                                // This section is longer than its length field alone specifies. Knowing the length of the remaining data requires quite a bit of in-depth parsing.
                                // Fortunately this data happens be one place that strictly follows the FF 00 escaping of FF bytes, allowing us to keep going until we find
                                // an FF XX sequence that isn't FF 00 or a restart (FF D0..D7, which are also lengthless and copied as-is without interrupting the FF DA section).
                                while (true)
                                {
                                    if (inp >= bytes.Length - 1) // all images must end with FF D9
                                        throw new Exception("unexpected end of JPEG file");
                                    if (bytes[inp] == 0xFF)
                                        if (bytes[inp + 1] != 0 && ((bytes[inp + 1] & 0xF8) != 0xD0))
                                            break; // exit the loop when a marker is encountered that isn't an FF 00 escape or an FF D0..D7 restart
                                    bytes[outp++] = bytes[inp++];
                                }
                            }
                        }
                        else if (next == 0 || ((next & 0xF8) == 0xD0)) // FF 00 escapes and restarts are only expected inside section data
                            throw new Exception("invalid or unsupported JPEG file");
                        else // all other sections are skipped using the length specifier
                        {
                            if (inp >= bytes.Length - 3)
                                throw new Exception("unexpected end of JPEG file");
                            inp += 2 + (bytes[inp + 2] << 8) + bytes[inp + 3];
                        }
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
                        f.Write(bytes, 0, 2);
                        var app0 = new byte[] { /* app0 marker */ 0xFF, 0xE0, /* length */ 0, 16, /* "JFIF\0" */ 0x4A, 0x46, 0x49, 0x46, 0, /* v1.2 */ 1, 2, /* dpi units */ 1, /* dpiX & Y */ 0, 1, 0, 1, /* thumbX & Y */ 0, 0 };
                        f.Write(app0, 0, app0.Length);
                        f.Write(bytes, 2, outp - 2);
                        f.Write(new byte[] { 0xFF, 0xD9 }, 0, 2); /* end of image marker */
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
