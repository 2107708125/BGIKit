﻿using System;
using System.IO;
using System.Text;

using Amemiya.Extensions;

namespace ScriptDecoder
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: ScriptDecoder.exe <script_file>");
                Console.ReadKey();
                return;
            }

            string fileInput = args[0];

            var br = new BinaryReader(new FileStream(fileInput, FileMode.Open));
            var bw =
                new StreamWriter(new FileStream(Path.GetFileNameWithoutExtension(fileInput) + ".txt",
                                                FileMode.Create));

            var scriptBuffer = br.ReadBytes((int) br.BaseStream.Length);
            br.Close();

            // headerLength includes MAGIC and @[0x1C].
            int headerLength = 0;

            // Check whether the file is in new format.
            // The most significant thing is the new format have the magic "BurikoCompiledScriptVer1.00\x00".
            // The difference between old and new is that, the old one DOES NOT have the HEADER which has
            // the length discribed at [0x1C] as a DWORD.
            if (
                scriptBuffer.Slice(0, 0x1C)
                            .EqualWith(new byte[]
                                {
                                    0x42, 0x75, 0x72, 0x69, 0x6B,
                                    0x6F, 0x43, 0x6F, 0x6D, 0x70,
                                    0x69, 0x6C, 0x65, 0x64, 0x53,
                                    0x63, 0x72, 0x69, 0x70, 0x74,
                                    0x56, 0x65, 0x72, 0x31, 0x2E,
                                    0x30, 0x30, 0x00
                                }))
            {
                headerLength = 0x1C + BitConverter.ToInt32(scriptBuffer, 0x1C);
            }
            // else headerLength = 0;

            // Remove HEADER.
            scriptBuffer = scriptBuffer.Slice(headerLength, scriptBuffer.Length);

            // Get the text offset.
            // The offset is always next to 0x00000001 (HEADER length not included).
            int firstTextOffset;
            int offset = scriptBuffer.IndexOf(new byte[] {1, 0, 0, 0}, 0, false);
            // Avoid possible overflow.
            if (offset < 0 || offset > 128)
            {
                firstTextOffset = 0;
            }
            else
            {
                firstTextOffset = BitConverter.ToInt32(scriptBuffer, offset + 4);
            }

            // Text offset is always next to 0x00000003.
            int intTextOffsetLabel = scriptBuffer.IndexOf(new byte[] {0, 3, 0, 0, 0}, 0, false);

            while (intTextOffsetLabel != -1 && intTextOffsetLabel < firstTextOffset)
            {
                // To get the actual offset, combine intTextOffsetLabel with 5.
                intTextOffsetLabel += 5;

                int intTextOffset = BitConverter.ToInt32(scriptBuffer, intTextOffsetLabel);
                // We should always do the check in case of the modification of some important control bytes.
                if (intTextOffset > firstTextOffset && intTextOffset < scriptBuffer.Length)
                {
                    // Look up the text in original buffer.
                    byte[] bytesTextBlock = scriptBuffer.Slice(intTextOffset,
                                                                   scriptBuffer.IndexOf(new byte[] {0x00},
                                                                                        intTextOffset, false));

                    if (bytesTextBlock != null)
                    {
                        // BGI treat 0x0A as new line.
                        string strText = Encoding.GetEncoding(932)
                                                 .GetString(bytesTextBlock)
                                                 .Replace("\n", @"\n");

                        bw.WriteLine("<{0},{1},{2}>{3}", intTextOffsetLabel, intTextOffset,
                                     bytesTextBlock.Length, strText);
                    }
                }

                // Search for the next.
                intTextOffsetLabel = scriptBuffer.IndexOf(new byte[] {0, 3, 0, 0, 0}, intTextOffsetLabel,
                                                          false);
            }

            bw.Close();
            //Console.ReadLine();
        }
    }
}
