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
using NdefLibrary;

namespace nfc_rw
{
    class Program
    {
        static Dictionary<String, String> APDU_commands = new Dictionary<string, string>(){
            {"get_card_uid","FF CA 00 00 00"},
            {"read_binary_blocks","FF B0 00 00 10"} //last two bytes = 1. block to start reading. 2. How many bytes to read
            };

        static void Main(string[] args)
        {
            ConsoleTraceListener consoleTraceListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleTraceListener);

            PCSCReader reader = new PCSCReader();
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

                    RespApdu testi = reader.Exchange(APDU_commands["read_binary_blocks"]);
                    if (testi.SW1SW2 == 0x9000)
                    {
                        Console.WriteLine("Binary_data  = 0x" + HexFormatting.ToHexString(testi.Data, true));
                    }

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
