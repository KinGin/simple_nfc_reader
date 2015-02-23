simple_nfc_reader
=================

A simple NFC-reader & (emulator in C#)

Emulation & targetmode stuff do not work yet.

This programs structure is quite horrible because so far
the developement has been with the mentality "Okay let's try this. It kinda works? Or not? What?"
I will refactor it and clean it up when those final bits and pieces start working.

This program uses Mopius NDEF library for NDEF parsing	 http://www.mopius.com/app/ndef-library-for-proximity-apis/
and PC/SC C# library from smart card magic 				 http://www.smartcard-magic.net/en/pc-sc-reader/csharppcsc-wrapper/

The program is unfinished but the final goal is to provide functioning and easy to use class for reading
NDEF records off tags and emulating NDEF tags on  the reader. 






