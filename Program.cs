﻿/* The purpose of this class is to provide and easy to use methods for reading
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
using System.Collections;


namespace nfc_rw
{
    class Program
    {

        private static PCSCReader reader; 

        //Dictionary holding APDU commmands
        //Not all of these are used in this
        //program but I left them intact for future use
        static Dictionary<String, String> APDU_commands = new Dictionary<string, string>(){
            {"get_card_uid","FF CA 00 00 00"},
            {"read_binary_blocks","FF B0 00 00 04"}, //last two bytes = 1. block to start reading. 2. How many bytes to read
            //{"set_target_mode", "D4 8C 00"}
            //The fuck is this command?
            {"set_target_mode", "FF, 00, 00, 00, 27, D4, 8C, 05, 04, 00, 12, 34, 56, 20, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00"}
        };

        /* <summary>
         * void parse_record
         * Constructs NDEF record from given byte array. Parsing fails if the NDEF tag is not valid.
         * </summary>
         * 
         * <remarks>
         * If you know that NDEF tag is legit all you have to worry about is passing the right chunk of 
         * bytes to the function. NDEF tag starts with "D1" byte and ends IN "FE" byte. 
         * </remarks>
         * 
         * //TODO: expand for all types of NDEF records
         */
        static void parse_record(NdefMessage ndefMessage)
        {
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

        
        /*<summary>
         * BYTE[] find_ndef()
         * This function returns Byte array containgin all bytes from
         * starting byte of NDEF "D1" record to the termination byte "FE"
         *</summary>
         * 
         * <remarks>
         * This function is naive and only tries to find the content
         * delimited by start and terminating bytes of NDEF:s
         * </remarks>
         * 
         * 
         */

        static Byte[] find_ndef()
        {
            RespApdu read_four_bytes;
            int byte_num = 0;
            String command;
            
            var allbytes = new List<string>();

            //Used for easy appending of all found bytes
            var hexbytes = new List<byte>();


            while (true)
            {
                int value = Convert.ToInt32(byte_num);
                String Hex_address = String.Format("{0:X}", value);
                
                //RespApdu only accepts commands that are of type
                //"XX XX XX XX XX" so zero has to be prepended
                //for values smaller than 0x10
                if (byte_num < 16)
                {
                    Hex_address = "0" + Hex_address;
                }

                //We start from block 0 byte 0
                //Reading command: "FF B0 00 XX YY"
                //XX is reading address
                //YY is amount of bytes to read
                //Distance between each XX is 04
                command = "FF B0 00 " + Hex_address + " 04";

                read_four_bytes = reader.Exchange(command);
                if (read_four_bytes.SW1SW2 != 0x9000)
                {
                    Console.WriteLine("Reading bytes from the NFC tag failed. reader returnerd: ", HexFormatting.ToHexString(read_four_bytes.Data, true));
                    break;
                }
                
                allbytes.Add(HexFormatting.ToHexString(read_four_bytes.Data, true));
                hexbytes.AddRange(read_four_bytes.Data);


                if (HexFormatting.ToHexString(read_four_bytes.Data, true).Contains("FE"))
                {
                    Console.WriteLine("End of NDEF found");
                    break;
                }
                byte_num = byte_num + 1;
            }

            foreach (Object obj in hexbytes)
                Console.Write("   {0}", String.Format("{0:X}", obj));
            Console.WriteLine();

            for (int i = 0; i < hexbytes.Count; i++)
            {
                //This IS D1 in hex. It starts NDEF tags
                if (hexbytes[i] == 209)
                {
                    hexbytes.RemoveRange(0, i);
                }
                else if (hexbytes[i] == 254)
                {
                    break;
                }
            }

            return hexbytes.ToArray();
        }

        static byte[] set_target_mode()
        {
            RespApdu respApdu = reader.Exchange(APDU_commands["set_target_mode"]);
            if (respApdu.SW1SW2 != 0x9000)
            {
                Console.WriteLine("What does this all mean?: ", HexFormatting.ToHexString(respApdu.Data, true));
            }
            return respApdu.Data;
        }


        static void Main(string[] args)
        {
            ConsoleTraceListener consoleTraceListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleTraceListener);

            reader = new PCSCReader();
            NdefLibrary.Ndef.NdefMessage message = new NdefLibrary.Ndef.NdefMessage();

            string input_text = "";
            while (input_text != "joo")
            {
                try
                {
                    reader.Connect();
                    reader.ActivateCard(SCARD_SHARE_MODE.Direct, SCARD_PROTOCOL.Tx);
                    

                    RespApdu respApdu = reader.Exchange(APDU_commands["get_card_uid"]); // Get Card UID ...

                    if (respApdu.SW1SW2 == 0x9000)
                    {
                        Console.WriteLine("UID  = 0x" + HexFormatting.ToHexString(respApdu.Data, true));
                    }

                    message = NdefLibrary.Ndef.NdefMessage.FromByteArray(find_ndef());
                    //message = NdefLibrary.Ndef.NdefMessage.FromByteArray(set_target_mode());
                    parse_record(message);
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
