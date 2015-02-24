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
        private static bool modes_changed = false;


       
        //Dictionary holding APDU commmands
        //Not all of these are used in this
        //program but I left them intact for future use
        static Dictionary<String, String> APDU_commands = new Dictionary<string, string>(){
            
            //These work with reader.Exchange()
            //Direct commands need the aproach shown in
            //direct_command function
            {"get_card_uid","FF CA 00 00 00"},
            {"read_binary_blocks","FF B0 00 00 04"}, //last two bytes = 1. block to start reading. 2. How many bytes to read
            {"update_binary_blocks","FF D6 00 00 04 {0}"}, //last 3 bytes = 1. block to start reading. 2. How many bytes to read, bytes in
            
            //############# Pseudo APDUS #############  
            //These are used for: 
            //1. Exchanging data with Non-PC/SC compliant tags
            //2. Retrieving and setting reader parameters - So we are trying to activate the "Connect as target and wait for initiators" here
            //3. Send through PICC interface if the tag is connected or through Escape command if not
            {"direct_command_prefix", "FF 00 00 00 {0} {1}"}, //{0} is the number of bytes to send, {1} is the payload
           
            //Get info about picc params
            //like polling interval, tags that are accepted etc.
            //see ACR122U-Api documentation for comprehensive list
            {"get_picc_operating_parameter","FF 00 50 00 00"},
            {"picc_polling_off", "FF 00 51 7F 00"},
            {"picc_polling_on", "FF 00 51 FF 00"},
            {"antenna_off", "FF 00 00 00 04 D4 32 01 00"},
            {"antenna_on", "FF 00 00 00 04 D4 32 01 01"},
            {"buzzer_on", "FF 00 52 00 00"},
            {"buzzer_off","FF 00 52 00 FF"},

            //############# Configure as target and wait for initiators #############
            //This set of commands used for initializing the smart card reader
            //into card emulation mode. Actually the communication is done with the PN532 chip inside the reader
            //You need to use this mode for data exchange with smartphones
            {"Tg_Init_as_target", "FF 00 00 00 D4 8C 00 " }, 
            {"Receive_data_from_initiator", "FF 00 00 00 D4 86" },
            {"Send_data_to_initiator", "FF 00 00 00 D4 8E {0}" },                   // {0} Data to be sent
            
            //Michael Roland sweet set for card emulation mode gogo:
            //source http://stackoverflow.com/questions/21051315/nfc-acr122-tginitastarget-initiator-releasing-target
            //http://quabr.com/21720557/nfc-card-emulation-issue-s3android-4-3-and-acr122u
            {"read_register","FF000000 08 D406 6305 630D 6338"},
            {"write_register","FF000000 11 D408 6302 80 6303 80 6305 04 630D EF 6338 F7"},
            {"set_params","FF000000 03 D412 30"},
            {"tg_init","FF00000027D48C05040012345620000000000000000000000000000000000000000000000000000000000000"}, //TGINIT DOES NOT GIVE RESPONSE
            {"tg_get_data","FF000000 02 D486"},
            {"tg_set_data","FF000000 {0} D48E {1}"}, //{0} = 2+length of {1}=Command sent from thhe reader

            //FROM the ACR122_PN53
            //https://code.google.com/p/nfcip-java/source/browse/trunk/nfcip-java/doc/ACR122_PN53x.txt
            {"injumpfordep","FF 00 00 00 0A D4 56 00 02 01 00 FF FF 00 00"},
            {"set_as_target","FF 00 00 00 09 D4 8C 00 08 00 12 24 56 40"}
            /*{"",""},
            {"",""},
            {"",""},
            {"",""},
            {"",""},
            */

            //{"read_register", "FF 00 00 00 08 D4 06 63 05 63 0D 63 38"}             // Get information about what state the reader is  
            //{"read_register", "FF 00 00 00 38 D4 06 63 05 63 0D 63 38 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"}

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
        static string parse_record(NdefMessage ndefMessage)
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
                    return spRecord.Uri;
                    // You can also check the action (if in use by the record), 
                    //mime type and size of the linked content.
                }
                else if (specializedType == typeof(NdefUriRecord))
                {
                    // Convert and extract URI info
                    var uriRecord = new NdefUriRecord(record);
                    Debug.WriteLine("URI: " + uriRecord.Uri);
                    return uriRecord.Uri;
                }
                else if (specializedType == typeof(NdefTextRecord))
                {
                    // Convert and extract Text record info
                    var textRecord = new NdefTextRecord(record);
                    Debug.WriteLine("Text: " + textRecord.Text);
                    Debug.WriteLine("Language code: " + textRecord.LanguageCode);
                    var textEncoding = (textRecord.TextEncoding == NdefTextRecord.TextEncodingType.Utf8 ? "UTF-8" : "UTF-16");
                    Debug.WriteLine("Encoding: " + textEncoding);
                    return textRecord.Text;
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
            String Hex_address;
            int value;

            var allbytes = new List<string>();

            //Used for easy appending of all found bytes
            var hexbytes = new List<byte>();


            while (true)
            {
                value = Convert.ToInt32(byte_num);
                Hex_address = String.Format("{0:X}", value);
                
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
                    Console.WriteLine("Reading bytes from the NFC tag failed. reader returned: ", HexFormatting.ToHexString(read_four_bytes.Data, true));
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


        //Currently this writes plaintext to tag in NDEF format
        //TODO: Other tag types
        //      Limit to how much data can be fit to tag
        static void write_to_tag(String bytes_in)
        {
            string command_prefix = "FF D6 00 ";
            string command;
            String Hex_address;

            NdefMessage write_this = new NdefMessage {new NdefTextRecord { LanguageCode = "fi", Text = bytes_in } };
            byte[] ndef_prefix = { 0x03, (byte)write_this.ToByteArray().Length };
            List<byte> concat = new List<byte>();

            concat.AddRange(ndef_prefix);
            concat.AddRange(write_this.ToByteArray());
            byte[] ndef_bytes = concat.ToArray();

            foreach (var item in ndef_bytes)
            {
                Console.WriteLine(HexFormatting.ToHexString(item));
            }

            //Byte[] Input_bytes = hex_string_to_byte_array(string_to_hex_string(bytes_in));
            byte[] send_chunk = new byte[4];
            RespApdu write_four_bytes;
            int count = 0;
            int mod4 = ndef_bytes.Length % 4;


            for (int i = 0; i < ndef_bytes.Length; ++i)
            {

                send_chunk[count] = ndef_bytes[i];
                ++count;
                if ( count == 4)
                {
                    count = 0;
                    Hex_address = String.Format("{0:X}", Convert.ToInt32( (i / 4) + 4));
                    if ( (i / 4) + 4 < 16)
                    {
                        Hex_address = "0" + Hex_address;
                    }
                    Console.WriteLine(Hex_address);

                    command = command_prefix + Hex_address + " 04 " + HexFormatting.ToHexString(send_chunk, true);
                    Console.WriteLine(command);
                    write_four_bytes = reader.Exchange(command);                  
                    //send_chunk.
                }
            }

            if (count != 0)
            {
                for (int i = count; i < send_chunk.Length; i++)
                {
                    send_chunk[i] = 0x00;
                }
                send_chunk[2] = 254;
                Hex_address = String.Format("{0:X}", Convert.ToInt32(((ndef_bytes.Length - count) / 4) + 4));
                if ((((ndef_bytes.Length - count) / 4) + 4) < 16)
                {
                    Hex_address = "0" + Hex_address;
                }
                command = command_prefix + Hex_address + " 04 " + HexFormatting.ToHexString(send_chunk, true);
                write_four_bytes = reader.Exchange(command);
            }



            //command = command + "04" + " 04 " + hexString;
            //write_four_bytes = reader.Exchange(command);
           
            /*if (Input_bytes.Length % 4 != 0)
            {
                int mod_of_input = Input_bytes.Length % 4;
                for (int i = 0; i < mod_of_input; i++)
                {
                    

                }
            }*/

/*
            for (int i = 0; i < Input_bytes.Length; i = i + 4)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (j + i < Input_bytes.Length)
                    {
                        send_chunk[j] = Input_bytes[j + i];
                    }
                }

                value = Convert.ToInt32(byte_num);
                Hex_address = String.Format("{0:X}", value);
                command = command + Hex_address + " 04 " + HexFormatting.ToHexString(send_chunk, true);
                write_four_bytes = reader.Exchange(command);
                byte_num = byte_num + 1;
            }*/
        }

        static void turn_off_antenna()
        {
            direct_command(APDU_commands["antenna_off"]);
        }
        static void turn_on_antenna()
        {
            direct_command(APDU_commands["antenna_on"]);
        }
        static void direct_command(string command)
        {
            //byte[] apdu = { 0xFF, 0x00, 0x50, 0x00, 0x00 };

            byte[] apdu = hex_string_to_byte_array(command);
            byte[] recBuffer = new byte[256];
            int recLen = recBuffer.Length;
            reader.SCard.Transmit(apdu, apdu.Length, recBuffer, ref recLen);

            /*foreach (var item in recBuffer)
            {
                Console.WriteLine(item.ToString());
            }*/
        }
        static void try_this()
        {
            direct_command(APDU_commands["injumpfordep"]);
            direct_command(APDU_commands["set_as_target"]);

            while (true)
            {
                System.Threading.Thread.Sleep(5000);
                direct_command(APDU_commands["tg_get_data"]);
            }
        }
        static void set_initiator_mode() 
        {
            //RespApdu get_data = reader.Exchange(APDU_commands["read_register"]);
            //string input_text = Console.ReadLine();
            direct_command(APDU_commands["read_register"]);
            direct_command(APDU_commands["write_register"]);
            direct_command(APDU_commands["set_params"]);
            direct_command(APDU_commands["tg_init"]);

            while (true)
            {
                System.Threading.Thread.Sleep(5000);
                direct_command(APDU_commands["tg_get_data"]);
            }
            
        }


        static void set_target_mode()
        {
            byte[] rbuffer = null;

            //byte[] sbuffer = {0xFF, 0x00, 0x00, 0x00, 0x0A, 0xD4, 0x56, 0x01, 0x02, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00};
            byte[] sbuffer = {0xFF, 0x00, 0x00, 0x00, 0x33, 0xD4, 0x8C, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x01, 0xFE, 0x0F, 0xBB, 0xBA, 0xA6, 0xC9, 0x89, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x01, 0xFE, 0x0F, 0xBB, 0xBA, 0xA6, 0xC9, 0x89, 0x00, 0x00, 0x0F, 0x46, 0x66, 0x6D, 0x01, 0x01, 0x10, 0x03, 0x02, 0x00, 0x01, 0x04, 0x01, 0x96};
            //sbuffer = new Byte[0xFF0000000AD45600020000FFFF0000];

            reader.Exchange(sbuffer);

            Console.WriteLine("Juma", rbuffer.Length);
            //RespApdu tassu = reader.Exchange(String.Format(APDU_commands["direct_command_prefix"], "0A", "D4 56 00 02 00 00 FF FF 00 00"));
            //Console.WriteLine("Joo eli:  ", HexFormatting.ToHexString(rbuffer, true));
        }

        // You only have to call these once to affect the whole session(until reader is unplugged)
        // There appears to be no "get buzzer state" apdu
        static void set_buzzer_off()
        {
            direct_command(APDU_commands["buzzer_off"]);
        }
        static void set_buzzer_on()
        {
            direct_command(APDU_commands["buzzer_on"]);
        }
        static void change_modes_for_reader()
        {
            //Buzzer Control
            //set_buzzer_off();
            //set_buzzer_on();

            //The mode of the reader
            //set_initiator_mode();
            set_target_mode();

        }


        static void Main(string[] args)
        {
            ConsoleTraceListener consoleTraceListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleTraceListener);
            List<string> arguments = new List<string>();
            arguments.AddRange(args);

            foreach (var item in arguments)
            {
                Console.WriteLine(item);
            }

            reader = new PCSCReader();
            NdefLibrary.Ndef.NdefMessage message = new NdefLibrary.Ndef.NdefMessage();

            //string input_text = "";
            //while (input_text != "joo")
            //{
                try
                {

                    turn_on_antenna();
                    //reader.SCard.Connect("",SCARD_SHARE_MODE.Direct, SCARD_PROTOCOL.Tx);
                    //reader.Connect();
                    //reader.ActivateCard();
                    //set_buzzer_on();
                    //set_target_mode();

                    if(args[0] == "read")
                    {
                        Console.WriteLine("Reading from tag");
                        reader.Connect();
                        reader.ActivateCard();
                        message = NdefLibrary.Ndef.NdefMessage.FromByteArray(find_ndef());
                        Console.WriteLine(parse_record(message));
                    }
                    else if (args[0] == "write")
                    {
                        Console.WriteLine("writing to tag: ", args[1]);
                        reader.Connect();
                        reader.ActivateCard();
                        write_to_tag(args[1]);
                    }
                    else
                    {
                        Console.WriteLine("no arguments given. Stopping....");
                    }

                    //write_to_tag(input_text = "Hassulla Tassulla kiva paijaa massua ai että kun on hassua:3");
                    //reader.ActivateCard();
                    //try_this();
                    //set_initiator_mode();
                    
                    
                    //For some reason Direct commands only work after card has been activated (the command above)
                    //Also the reader resets to normal state after it's been unplugged.
                    //TODO: check if mode changes can be made permanent

                    //RespApdu respApdu = reader.Exchange(APDU_commands["get_card_uid"]); // Get Card UID ...

                    /*if (respApdu.SW1SW2 == 0x9000)
                    {
                        Console.WriteLine("UID  = 0x" + HexFormatting.ToHexString(respApdu.Data, true));
                    }*/

                    //message = NdefLibrary.Ndef.NdefMessage.FromByteArray(find_ndef());
                    //parse_record(message);
                    System.Environment.Exit(1);
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
                    //direct_command(APDU_commands["enable_picc_polling"]);
                    
                    reader.SCard.Disconnect();
                    turn_off_antenna();
                    System.Environment.Exit(1);
                    //Console.WriteLine("Please press any key...");
                    //input_text = Console.ReadLine();
                }
            }
        //}

        static byte[] hex_string_to_byte_array(String hex_string)
        {
            hex_string = hex_string.Replace(" ", "");
            int NumberChars = hex_string.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex_string.Substring(i, 2), 16);
            return bytes;
        }

        static string string_to_hex_string(string input)
        {
            byte[] temp = Encoding.Default.GetBytes(input);
            var hexString = BitConverter.ToString(temp);
            hexString = hexString.Replace("-", " ");
            return hexString;
        }


        static List<byte[]> splitted_array(byte[] array, int lengthToSplit)
        {
            List<byte[]> splitted = new List<byte[]>();//This list will contain all the splitted arrays.

            int arrayLength = array.Length;

            for (int i = 0; i < arrayLength; i = i + lengthToSplit)
            {
                Byte[] val = new Byte[lengthToSplit];

                if (arrayLength < i + lengthToSplit)
                {
                    lengthToSplit = arrayLength - i;
                }
                Array.Copy(array, i, val, 0, lengthToSplit);
                splitted.Add(val);
            }
            return splitted;
        }
    } 
}
