using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GS.Apdu;
using GS.PCSC;
using GS.SCard;
using GS.SCard.Const;
using GS.Util.Hex;
using System.Diagnostics;
using NdefLibrary.Ndef;

namespace nfc_rw
{
    class Program
    {
        static Dictionary<String, String> APDU_commands = new Dictionary<string, string>(){
            {"get_card_uid","FF CA 00 00 00"},
            {"read_first_binary_blocks","FF B0 00 07 10"},//NDEF RECORD STARTS AT 7 O'Block 
            {"read_binary_blocks","FF B0 00 0B 02"}, //last two bytes = 1. block to start reading. 2. How many bytes to read
            {"read_all_memory_content", "FF B0 00 0C 10" }
        };

        static void Main(string[] args)
        {
            ConsoleTraceListener consoleTraceListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleTraceListener);

            PCSCReader reader = new PCSCReader();
            NdefLibrary.Ndef.NdefMessage message = new NdefLibrary.Ndef.NdefMessage();

            byte[] whole_NFC;


            string input_text = "";
            while (input_text != "joo")
            {
                try
                {
                    reader.Connect();
                    reader.ActivateCard();

                    RespApdu respApdu = reader.Exchange(APDU_commands["get_card_uid"]); // Get Card UID ...

                    if (respApdu.SW1SW2 == 0x9000)
                    {
                        Console.WriteLine("UID  = 0x" + HexFormatting.ToHexString(respApdu.Data, true));
                    }

                    RespApdu testi = reader.Exchange(APDU_commands["read_first_binary_blocks"]);
                    if (testi.SW1SW2 == 0x9000)
                    {
                        Console.WriteLine("Binary_data  = 0x" + HexFormatting.ToHexString(testi.Data, true));
                    }



                     RespApdu testi2 = reader.Exchange(APDU_commands["read_binary_blocks"]);
                     if (testi2.SW1SW2 == 0x9000)
                     {
                         Console.WriteLine("Binary_data  = 0x" + HexFormatting.ToHexString(testi2.Data, true));
                     }
                    whole_NFC = new byte[testi.Data.Length + testi2.Data.Length];
                    System.Buffer.BlockCopy(testi.Data, 0, whole_NFC, 0, testi.Data.Length);
                    System.Buffer.BlockCopy(testi2.Data, 0, whole_NFC, testi.Data.Length, testi2.Data.Length);
                    message = NdefLibrary.Ndef.NdefMessage.FromByteArray(whole_NFC);
                    
                    
                    
                    foreach (NdefRecord record in message)
                    {
                        Console.WriteLine(" Hurraa :DDDD");
                        Console.WriteLine("Record type: " + Encoding.UTF8.GetString(record.Type, 0, record.Type.Length));
                        var uriRecord = new NdefUriRecord(record);
                        Debug.WriteLine("URI: " + uriRecord.Uri);
                    }

                    /** 
                     * 
                     RespApdu testi3 = reader.Exchange(APDU_commands["read_all_memory_content"]);
                     if (testi3.SW1SW2 == 0x9000)
                     {
                         Console.WriteLine("Binary_data  = 0x" + HexFormatting.ToHexString(testi3.Data, true));
                     }*/

                    /*whole_NFC = new byte[testi.Data.Length + testi2.Data.Length + testi3.RespLength];
                    System.Buffer.BlockCopy(testi.Data, 0, whole_NFC, 0, testi.Data.Length);
                    System.Buffer.BlockCopy(testi2.Data, 0, whole_NFC, testi.Data.Length, testi2.Data.Length);
                    System.Buffer.BlockCopy(testi3.Data, 0, whole_NFC, testi.Data.Length + testi2.Data.Length, testi3.Data.Length);

                    message = NdefLibrary.Ndef.NdefMessage.FromByteArray(whole_NFC);
                    Console.WriteLine(message.GetType());*/
                    
                }
                catch (WinSCardException ex)
                {
                    Console.WriteLine(ex.WinSCardFunctionName + " Error 0x" +
                                       ex.Status.ToString("X08") + ": " + ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    reader.Disconnect();
                    Console.WriteLine("Please press any key...");
                    input_text = Console.ReadLine();
                }
            }
        }
    }
}
