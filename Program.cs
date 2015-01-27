/* The purpose of this class is to provide and easy to use methods for reading
 * and emulating NFC tags through PC/SC interface. In particular it is ment
 * to be used with NDEF formatted tags
 * 
 * Summary of the functionality of this class:
 * 
 * 1. PC/SC library detects if there is tag present near the reader
 * if there is the library handles the ATR stuff for us
 * 
 * 2. Class reads the payload present on the card and uses NDEF
 * library to determine what kind of NDEF message was written in the tag
 * 
 * 3. 
 * 
 * The libraries used are NDEFLibrary URL:
 * and the other one URL:
 * 
 * If you are newbie to the NFC tags and NDEF like I was here is some
 * basic info.
 * 
 * There is a large amount of APDU commands but for simple use
 * the bare neccesities are READ an WRITE which I have used below
 * 
 * The UID of the card tells basic information about what type of card it is:
 * //FIXME MORE DETAIL
 * 
 * I'm not sure if this is really the best way to do these
 * 
 * 
 */


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


        static void parse_record(NdefMessage ndefMessage)
        {

            //NdefRecord found_record;

            foreach (NdefRecord record in ndefMessage)
            {
                Debug.WriteLine("Record type: " + Encoding.UTF8.GetString(record.Type, 0, record.Type.Length));
                // Check the type of each record - handling a Smart Poster, URI and Text record in this example
                var specializedType = record.CheckSpecializedType(false);
                if (specializedType == typeof(NdefSpRecord))
                {
                    // Convert and extract Smart Poster info
                    var spRecord = new NdefSpRecord(record);
                    Debug.WriteLine("URI: " + spRecord.Uri);
                    Debug.WriteLine("Titles: " + spRecord.TitleCount());
                    Debug.WriteLine("1. Title: " + spRecord.Titles[0].Text);
                    Debug.WriteLine("Action set: " + spRecord.ActionInUse());
                    // You can also check the action (if in use by the record), 
                    //mime type and size of the linked content.
                }
                else if (specializedType == typeof(NdefUriRecord))
                {
                    // Convert and extract URI info
                    var uriRecord = new NdefUriRecord(record);
                    Debug.WriteLine("URI: " + uriRecord.Uri);
                }
                else if (specializedType == typeof(NdefTextRecord))
                {
                    // Convert and extract Text record info
                    var textRecord = new NdefTextRecord(record);
                    Debug.WriteLine("Text: " + textRecord.Text);
                    Debug.WriteLine("Language code: " + textRecord.LanguageCode);
                    var textEncoding = (textRecord.TextEncoding == NdefTextRecord.TextEncodingType.Utf8 ? "UTF-8" : "UTF-16");
                    Debug.WriteLine("Encoding: " + textEncoding);
                }
            }

        } 


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

                    parse_record(message);
                    
                   /* foreach (NdefRecord record in message)
                    {
                        Console.WriteLine(" Hurraa :DDDD");
                        Console.WriteLine("Record type: " + Encoding.UTF8.GetString(record.Type, 0, record.Type.Length));


                        var uriRecord = new NdefUriRecord(record);
                        Debug.WriteLine("URI: " + uriRecord.Uri);
                    }*/

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
