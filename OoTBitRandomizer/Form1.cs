using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TwitchLib;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication;
using System.Data;
using System.Text;
using System.Data.SQLite;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;

namespace OoTBitRaceRandomizer
{
    public partial class Form1 : Form
    {
        private TwitchClient client = null;
        private JoinedChannel channel = null;
        private Thread ConstantSetThread = null; private Thread freezeSetThread = null;
        private bool ConstantSetThreadEnabled = false; private bool freezeThreadEnabled = false;
        private int freezeTimer = 0; private bool freezerBurned = false;

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", EntryPoint = "ReadProcessMemory", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int ReadProcessMemory1(int hProcess, int lpBaseAddress, ref int lpBuffer, int nSize, ref int lpNumberOfBytesRead);

        private byte[] KokiriTunicColor = new byte[3] { 0x1E, 0x69, 0x1B }; // RGB
        private byte[] GoronTunicColor = new byte[3] { 0x64, 0x14, 0x00 }; // RGB
        private byte[] ZoraTunicColor = new byte[3] { 0x00, 0x3C, 0x64 }; // RGB
        private byte[] OcarinaSound = new byte[1] { 1 }; // 1 - 7


        private void SetConstantValues() {
            if (baseAddress != 0 && processHandle != null) {
                while (ConstantSetThreadEnabled && IsOoTAlive()) {
                    WriteCode(OcarinaSound, 1, 0x10220C);
                    WriteCode((byte)KokiriTunicColor[0], 1, 0x0F7AD8); WriteCode((byte)KokiriTunicColor[1], 1, 0x0F7AD9); WriteCode((byte)KokiriTunicColor[2], 1, 0x0F7ADA);
                    WriteCode((byte)GoronTunicColor[0], 1, 0x0F7ADB); WriteCode((byte)GoronTunicColor[1], 1, 0x0F7ADC); WriteCode((byte)GoronTunicColor[2], 1, 0x0F7ADD);
                    WriteCode((byte)ZoraTunicColor[0], 1, 0x0F7ADE); WriteCode((byte)ZoraTunicColor[1], 1, 0x0F7ADF); WriteCode((byte)ZoraTunicColor[2], 1, 0x0F7AE0);
                    Thread.Sleep(200); }
                Console.WriteLine("Constant Set Thread has ended!");
                ConstantSetThreadEnabled = false; }
        }

        private bool IsOoTAlive() {
            if (processHandle != null && baseAddress != 0) { 
                int reference = 0; int vBuffer = 0; dynamic NewDataValueBuffer = new byte[1];
                ReadWritingMemory.ReadProcessMemory1((int)processHandle, baseAddress + 0x10, ref vBuffer, 4, ref reference);
                //vBuffer = ReadCode(NewDataValueBuffer, 1, 0x003BCC);
                //if (vBuffer == 143) {  return true; } 
                if (ReadWritingMemory.ConstVal1 == vBuffer) { return true; }
            }
            Console.WriteLine("OoT emulation has ended!");
            return false;
        }

        private void IsLinkFrozen() {
            if (baseAddress != 0 && processHandle != null) {
                while (freezeThreadEnabled == true && IsOoTAlive()) { freezeTimer--; dynamic NewDataValueRead = new byte[1];

                    if (freezerBurned == true) { WriteCode((byte)1, 1, 0x1DB480); 
                        for (int i = 0; i < 20; i++) {
                            if (i % 2 == 0) { WriteCode((byte)255, 1, 0x1DB480 + (byte)i); }
                        else { WriteCode((byte)128, 1, 0x1DB480 + (byte)i);  } }
                        if (freezeTimer == 50) { WriteCode((byte)60, 1, 0x1DAB41); }
                    }

                    if (freezeTimer % 10 == 0) {
                        int curHealth = ReadCode(NewDataValueRead, 1, 0x11A601); int newHealth = 0; //Current Health value
                        if (curHealth < 3) { newHealth = 0; }
                        else {
                           if (freezerBurned == true) { newHealth = curHealth - 3; }
                           else {newHealth = curHealth - 1; } }
                        WriteCode((byte)newHealth, 1, 0x11A601); }

                    if (freezeTimer <= 0) { freezeThreadEnabled = false;
                        if (freezerBurned == true) { freezerBurned = false; //WriteCode((byte)0, 1, 0x1DB0A2);
                        } }
                    Thread.Sleep(50);
                } }
        }

        public Form1() { InitializeComponent(); }

        private void SetStatus(string Message) { StatusLabel.Text = Message; }

        public bool enableMessageGet;
        public IntPtr processHandle;
        public int baseAddress = 0;

        private int WriteCode(dynamic NewDataValue, int DataSize, int MemoryOffset) {

            if (DataSize == 4) { NewDataValue = NewDataValue.ToN64(); }
            byte[] WriteBuffer = null;

            if (NewDataValue.GetType() == typeof(byte)) { WriteBuffer = new byte[1] { NewDataValue }; }
            else if (NewDataValue.GetType() == typeof(byte[]))
            { var Data = (byte[])NewDataValue;
                WriteBuffer = new byte[Data.Length];
                Array.Copy(Data, WriteBuffer, WriteBuffer.Length); }
            else {  WriteBuffer = BitConverter.GetBytes(NewDataValue); }

            if (WriteBuffer.Length > 2) {  Array.Reverse(WriteBuffer); }
            int BytesWritten = 0;
            int Offset = MemoryOffset;
            
            // Reverse Memory Write Offset
            if (WriteBuffer.Length < 4) { Offset = ((Offset & ~3) | (3 - Offset & 3)) - (WriteBuffer.Length - 1); }

            WriteProcessMemory((int)processHandle, baseAddress + Offset, WriteBuffer, WriteBuffer.Length, ref BytesWritten);
            return BytesWritten;
        }

        private int ReadCode(dynamic NewDataValue, int DataSize, int MemoryOffset) {

            if (DataSize == 4) { NewDataValue = NewDataValue.ToN64(); }
            int[] ReadBuffer = null;

            if (NewDataValue.GetType() == typeof(byte)) { ReadBuffer = new int[1] { NewDataValue }; }
            else if (NewDataValue.GetType() == typeof(byte[]))
            {
                var Data = (byte[])NewDataValue;
                ReadBuffer = new int[Data.Length];
                Array.Copy(Data, ReadBuffer, ReadBuffer.Length);
            }
            else { ReadBuffer = BitConverter.GetBytes(NewDataValue); }

            if (ReadBuffer.Length > 2) { Array.Reverse(ReadBuffer); }
            int BytesRead = 0;
            int reference = 0;
            int Offset = MemoryOffset;

            // Reverse Memory Write Offset
            if (ReadBuffer.Length < 4) { Offset = ((Offset & ~3) | (3 - Offset & 3)) - (ReadBuffer.Length - 1); }

            ReadProcessMemory1((int)processHandle, baseAddress + Offset, ref BytesRead, ReadBuffer.Length, ref reference);
            return BytesRead;
        }

        private void button1_Click(object sender, EventArgs e) {

            if (client == null) {
                try {
                    ConnectionCredentials credentials = new ConnectionCredentials(textBox1.Text, textBox2.Text);
                    client = new TwitchClient(); channel = new JoinedChannel(textBox3.Text);
                    client.Initialize(credentials, textBox3.Text.ToLower());

                    client.OnMessageReceived += onMessageReceived;
                    client.OnJoinedChannel += Client_OnJoinedChannel;
                    client.Connect();

                    textBox1.Enabled = false; textBox2.Enabled = false; textBox3.Enabled = false;
                    button1.Enabled = false; button4.Enabled = false;

                    SetStatus("Successfully connected to Twitch Account: " + client.TwitchUsername);
                } catch { MessageBox.Show("There was an error connecting to your Twitch Chat!\r\nPlease check your login credentials.",
                        "Twitch Login Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void onMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Message.ToLower() == ("!pointrando")) {
                SendMessage(channel, "This is an OoT Point Randomizer run! This means that for using Mp16points, something in the game will change! " + //client.SendMessage(
                    "Further information can be found here: https://pastebin.com/fQ5n18ry"); }

            else if (e.ChatMessage.Message.ToLower().StartsWith("!mp ") || e.ChatMessage.Message.ToLower().StartsWith(">mp "))  {

                dynamic NewDataValueBuffer = new byte[1]; int OoTBuffer = ReadCode(NewDataValueBuffer, 1, 0x3BCC);
                int OoTinFileMenu = ReadCode(NewDataValueBuffer, 1, 0x11B92F);
               
                if (OoTBuffer != 143) { SendMessage(channel, string.Format("Ocarina of Time is not currently running! No Mp16points were used, " +
                    "please wait until everything is ready. =)")); }
                else if (OoTinFileMenu == 2) { SendMessage(channel, string.Format("An Ocarina of Time game file is currently not loaded! No Mp16points were used, " +
                    "please try another command! =)")); }
                else { 
                    
                //string userCommand = Regex.Match(e.ChatMessage.Message.Substring(4), @"\w+").Value;
                string[] fullUserCommand = e.ChatMessage.Message.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                //string userPoints = Regex.Match(e.ChatMessage.Message.Substring(4), @"\d+").Value; if (userPoints == "") { userPoints = "0"; }
                string userCommand; if (fullUserCommand.Length > 1) { userCommand = fullUserCommand[1].ToLower(); } else { userCommand = ""; }
                string user2ndCommand; if (fullUserCommand.Length > 2) { user2ndCommand = fullUserCommand[2].ToLower(); } else { user2ndCommand = ""; }
                string user3rdCommand; if (fullUserCommand.Length > 3) { user3rdCommand = fullUserCommand[3].ToLower(); } else { user3rdCommand = ""; }

                var ReturnedTuple = BitDonationManager.GetRequestedCode(userCommand); //, Int32.Parse(userPoints));
                CodeInfo selectedCode = ReturnedTuple.Item1;
                int resultCode = ReturnedTuple.Item2;
               
                /*if (resultCode == -1) {
                    int pointsRequired = BitDonationManager.GetPointsRequiredForCodeByCommandName(userCommand);
                    SendMessage(channel, string.Format("The code you requested requires {0} Mp16point{1} and you input {2}. " +
                    "No Mp16points were used, please try again!", pointsRequired, pointsRequired == 1 ? "" : "s", userPoints)); userPoints = "0"; }*/
                if (resultCode != 1) {
                    SendMessage(channel, string.Format("A code whose command is {0} was not found. No Mp16points were used, " +
                    "please try again!", userCommand)); }// userPoints = "0"; }
                else { //If Code Format was accepted, now time to check if user has enough Mp16Points

                SQLiteConnection SQLconn = new SQLiteConnection("Data Source=C:/Users/Brian/Desktop/OoT-Randomizer-master/" +
                "PhantomBot-2.4.2/PhantomBot-2.4.2/config/phantombot.db");
                SQLconn.Open();
                SQLiteCommand SQLcmdRead = new SQLiteCommand();
                SQLcmdRead.Connection = SQLconn; SQLcmdRead.CommandText = "Select * from phantombot_points";

                using (SQLiteDataReader SQLdr = SQLcmdRead.ExecuteReader()) {
                    SQLdr.Read(); DataTable SQLdt = new DataTable();
                    SQLdt.Load(SQLdr); SQLdr.Close(); SQLconn.Close();
                    string SQLdtUserName; int SQLdtPoints = 0;

                    for (int row = 0; row < SQLdt.Rows.Count; row++) {
                        SQLdtUserName = SQLdt.Rows[row]["variable"].ToString(); SQLdtPoints = Int32.Parse(SQLdt.Rows[row]["value"].ToString());

                        if (SQLdtUserName == e.ChatMessage.Username.ToString()) {

                                int pointsRequired=0;
                                if (fullUserCommand.Length > 2 && selectedCode.CommandName == "tunic" || fullUserCommand.Length > 3 && selectedCode.CommandName == "weapon")
                                    { pointsRequired = BitDonationManager.GetPointsRequiredForCodeByCommandName(userCommand + "2"); }
                                else if (user2ndCommand == "0" && selectedCode.CommandName != "ocarina" && selectedCode.CommandName != "weapon" &&
                                     selectedCode.CommandName != "time" && selectedCode.CommandName != "burn" && selectedCode.CommandName != "freeze" &&
                                     selectedCode.CommandName != "freezeburn" && selectedCode.CommandName != "freezerburn") { pointsRequired = 5; }
                                else { pointsRequired = BitDonationManager.GetPointsRequiredForCodeByCommandName(userCommand); }

                                if (SQLdtPoints >= pointsRequired) {

                                    if (baseAddress != 0) { bool codePassed = true; int pointsRemain = SQLdtPoints - pointsRequired;

                                        if (selectedCode != null) {

                                            if (selectedCode.Name.Equals("Current B Button Weapon")) {
                                                //Random B Button assigned to 1: Kokiri Sword 2: Master Sword 3: Giant's Knife 4: Biggoron's Sword
                                                //5: SOLD OUT 6: Megaton Hammer 7: Deku Nuts 8: Deku Sticks as Child, Ice Arrows as Adult
                                                //BONUS update flag for Biggoron's Sword vs. Giant's Knife and update flag for Broken Knife or not

                                                //Need check for if Link is in a mini-game or on a horse
                                                //Bombchu Bowling=9, Fishing=89, Archery/Epona=3, Slingshot game=6, 

                                                dynamic NewDataValueRead = new byte[1];
                                                int currentState = ReadCode(NewDataValueRead, selectedCode.DataSize, selectedCode.MemoryOffset);
                                                
                                                if (currentState == 6) { codePassed = false;
                                                SendMessage(channel, string.Format("{0} tried to change the {1}, but BrianMp16 is currently playing the " +
                                                "Shooting Gallery Slingshot mini-game! No Mp16points were used,  please try another command! =)", 
                                                e.ChatMessage.Username, selectedCode.Name)); }
                                                else if (currentState == 3) { codePassed = false;
                                                SendMessage(channel, string.Format("{0} tried to change the {1}, but BrianMp16 is currently on Epona " +
                                                "or in a mini-game! No Mp16points were used,  please try another command! =)", 
                                                e.ChatMessage.Username, selectedCode.Name)); }
                                                else if (currentState == 9) { codePassed = false;
                                                SendMessage(channel, string.Format("{0} tried to change the {1}, but BrianMp16 is currently Bombchu " +
                                                "Bowling! No Mp16points were used,  please try another command! =)", 
                                                e.ChatMessage.Username, selectedCode.Name)); }
                                                else if (currentState == 89) { codePassed = false;
                                                SendMessage(channel, string.Format("{0} tried to change the {1}, but BrianMp16 is currently Fishing! " +
                                                "No Mp16points were used,  please try another command! =)", 
                                                e.ChatMessage.Username, selectedCode.Name)); }
                                                else if (currentState == 255) { codePassed = false;
                                                SendMessage(channel, string.Format("{0} tried to change the {1}, but BrianMp16 currently has nothing on B! =( " +
                                                "No Mp16points were used,  please try another command! =)", 
                                                e.ChatMessage.Username, selectedCode.Name)); }

                                                else {
                                                    string weaponChoice = user2ndCommand + " " + user3rdCommand;
                                                    int BButtonValue = 0; string BButtonName = ""; int itemSelect = -1;
                                                    //int currentAge = ReadCode(NewDataValueRead, 1, selectedCodeCheck.MemoryOffset);

                                                    switch (weaponChoice) {
                                                        case " ": Random rnd = new Random(); itemSelect = rnd.Next(1, 8); break;
                                                        case "kokiri ": itemSelect = 1; break; case "kokiri sword": itemSelect = 1; break; case "kokiri's sword": itemSelect = 1; break; case "kokiris sword": itemSelect = 1; break;
                                                        case "master ": itemSelect = 2; break; case "master sword": itemSelect = 2; break; case "master's sword": itemSelect = 2; break; case "masters sword": itemSelect = 2; break;
                                                        case "giant ": itemSelect = 3; break; case "giant's knife": itemSelect = 3; break; case "giants knife": itemSelect = 3; break; case "giant knife": itemSelect = 3; break;
                                                        case "bgs ": itemSelect = 4; break; case "biggoron ": itemSelect = 4; break; case "biggoron's sword": itemSelect = 4; break; case "biggorons sword": itemSelect = 4; break; case "biggoron sword": itemSelect = 4; break;
                                                        case "soldout ": itemSelect = 5; break; case "sold out": itemSelect = 5; break;
                                                        case "broken knife": itemSelect = 6; break; case "broken giant's": itemSelect = 6; break; case "broken giants": itemSelect = 6; break; case "broken giant": itemSelect = 6; break;
                                                        case "hammer ": itemSelect = 7; break; case "megaton hammer": itemSelect = 7; break; case "megaton's hammer": itemSelect = 7; break; case "megatons hammer": itemSelect = 7; break;
                                                        case "nuts ": itemSelect = 8; break; case "deku nuts": itemSelect = 8; break; case "deku nut": itemSelect = 8; break;
                                                        case "boomerang ": itemSelect = 9; break;
                                                        case "lens ": itemSelect = 10; break; case "lens of": itemSelect = 10; break;
                                                        case "beans ": itemSelect = 11; break; case "magic beans": itemSelect = 11; break;
                                                        case "fire ": itemSelect = 12; break; case "fire arrows": itemSelect = 12; break; case "fire arrow": itemSelect = 12; break;
                                                        case "ice ": itemSelect = 13; break; case "ice arrows": itemSelect = 13; break; case "ice arrow": itemSelect = 13; break;
                                                        case "light ": itemSelect = 14; break; case "light arrows": itemSelect = 14; break; case "light arrow": itemSelect = 14; break;
                                                        default: itemSelect = -1; break; }
                                                   
                                                    switch (itemSelect) {
                                                        case -1: codePassed = false;
                                                        SendMessage(channel, string.Format("{0} is not an available B Button Weapon option. No Mp16points were used, " +
                                                        "please try again! =)", weaponChoice)); break;
                                                        case 1: BButtonValue = 59; BButtonName = "Kokiri Sword"; break;
                                                        case 2: BButtonValue = 60; BButtonName = "Master Sword"; break;
                                                        case 3: BButtonValue = 61; BButtonName = "Giant's Knife";
                                                            WriteCode((byte)8, selectedCode.DataSize, 0x11A607);
                                                            WriteCode((byte)0, selectedCode.DataSize, 0x11A60E); break;
                                                        case 4: BButtonValue = 61; BButtonName = "Biggoron's Sword";
                                                            WriteCode((byte)8, selectedCode.DataSize, 0x11A607);
                                                            WriteCode((byte)1, selectedCode.DataSize, 0x11A60E); break;
                                                        case 5: BButtonValue = 44; BButtonName = "SOLD OUT"; break;
                                                        case 6: BButtonValue = 85; BButtonName = "Broken Giant's Knife"; break;
                                                        case 7: BButtonValue = 17; BButtonName = "Megaton Hammer"; break;
                                                        case 8: BButtonValue = 1; BButtonName = "Deku Nuts"; break;
                                                        case 9: BButtonValue = 14; BButtonName = "Boomerang"; break;
                                                        case 10: BButtonValue = 15; BButtonName = "Lens of Truth"; break;
                                                        case 11: BButtonValue = 16; BButtonName = "Magic Beans"; break;
                                                        case 12: BButtonValue = 4; BButtonName = "Fire Arrows"; break;
                                                        case 13: BButtonValue = 12; BButtonName = "Ice Arrows"; break;
                                                        case 14: BButtonValue = 18; BButtonName = "Light Arrows"; break;
                                                        default: BButtonValue = 59; break; }

                                                    if (codePassed == true) { 
                                                    WriteCode((byte)BButtonValue, selectedCode.DataSize, selectedCode.MemoryOffset); 

                                                    SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Weapon changed to: {4}. " +
                                                    "{0} now has {5} Mp16point{6} remaining! =)", e.ChatMessage.Username, pointsRequired,
                                                    pointsRequired == 1 ? "" : "s", selectedCode.Name, BButtonName, pointsRemain,
                                                    pointsRemain == 1 ? "" : "s")); }
                                                }
                                            }

                                        else if (selectedCode.Name.Equals("Link on Fire")) {
                                            dynamic NewDataValueRead = new byte[1];
                                            //int curStateFreeze = ReadCode(NewDataValueRead, 1, 0x1DAB41); //If Link is Frozen in Animation address
                                            int curStateFreeze = ReadCode(NewDataValueRead, 1, 0x406ED4); //If Link is Frozen in Ice address
                                            int curStateBurn = ReadCode(NewDataValueRead, 1, 0x1DB480); //If Link is on Fire

                                            if (curStateFreeze > 0) { codePassed = false;
                                            SendMessage(channel, string.Format("{0} tried to attack Link with flames >=( but Link is currently in a state where he cannot be lit on fire. " +
                                            "No Mp16points were used, please try another command! =)", e.ChatMessage.Username)); }

                                            else if (curStateBurn == 1) { codePassed = false;
                                            SendMessage(channel, string.Format("{0} tried to attack Link with flames >=( but Link is already on fire! " +
                                            "No Mp16points were used, please try another command! =)", e.ChatMessage.Username)); }

                                            else { 
                                            int curHealth = ReadCode(NewDataValueRead, 1, 0x11A601); int newHealth = 0; //Current Health value
                                            if (curHealth <= 4 ) { newHealth = 0; } else { newHealth = curHealth - 4; }
                                            string newHealthStr = NewHealthFraction(newHealth);

                                            WriteCode((byte)1, selectedCode.DataSize, selectedCode.MemoryOffset);
                                            WriteCode((byte)newHealth, selectedCode.DataSize, 0x11A601);

                                            try { freezeThreadEnabled = true; freezeTimer = 10; freezerBurned = true;
                                                freezeSetThread = new Thread(IsLinkFrozen); freezeSetThread.Start(); }
                                            catch { } // SendMessage(channel, "freeze failed =("); }

                                            Random rnd = new Random(); 
                                            for (int i=0; i < 20; i++) { int burnIntensity = rnd.Next(10, 192);
                                            WriteCode((byte)burnIntensity, selectedCode.DataSize, selectedCode.MemoryOffset + (byte)i); }

                                            SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Link is now on fire! >=( " +
                                            "Health remaining: {4}{5} heart{6}. {0} now has {7} Mp16point{8} remaining! =)", e.ChatMessage.Username, pointsRequired,
                                            pointsRequired == 1 ? "" : "s", selectedCode.Name, newHealth/16, newHealthStr, newHealth/16 == 1 && newHealthStr == "" ? "" : "s",
                                            pointsRemain, pointsRemain == 1 ? "" : "s")); }
                                        }

                                        else if (selectedCode.Name.Equals("Link Frozen")) {
                                            dynamic NewDataValueRead = new byte[1];
                                            //int curStateFreeze = ReadCode(NewDataValueRead, 1, 0x1DAB41); //If Link is Frozen in Animation address
                                            int curStateFreeze = ReadCode(NewDataValueRead, 1, 0x406ED4); //If Link is Frozen in Ice address
                                            int curStateBurn = ReadCode(NewDataValueRead, 1, 0x1DB480); //If Link is on Fire
                                            //int curStateLocked = ReadCode(NewDataValueRead, 1, 0x1DB09C); //If Camera is Locked

                                            if (curStateFreeze > 0) { codePassed = false;
                                            SendMessage(channel, string.Format("{0} tried to use a freeze attack >=( but Link is already frozen! " +
                                            "No Mp16points were used, please try another command! =)", e.ChatMessage.Username)); }

                                            //else if (curStateLocked == 32 || curStateBurn == 1) { codePassed = false;
                                            //SendMessage(channel, string.Format("{0} tried to use a freeze attack >=( but Link is currently in a state where he cannot be frozen. " +
                                            //"No Mp16points were used, please try another command! =)", e.ChatMessage.Username)); }

                                            else { 
                                                int curHealth = ReadCode(NewDataValueRead, 1, 0x11A601); int newHealth = 0; //Current Health value
                                                if (curHealth <= 6) { newHealth = 0; } else { newHealth = curHealth - 3; }
                                                string newHealthStr = NewHealthFraction(newHealth);

                                            //WriteCode((byte)70, selectedCode.DataSize, selectedCode.MemoryOffset);
                                            WriteCode((byte)newHealth, selectedCode.DataSize, 0x11A601);  //New Health
                                            WriteCode((byte)1, selectedCode.DataSize, 0x406ED4);  // Link in Ice
                                                
                                            //try { freezeThreadEnabled = true; freezeTimer = 70;
                                            //    freezeSetThread = new Thread(IsLinkFrozen); freezeSetThread.Start(); }
                                            //catch { } // SendMessage(channel, "freeze failed =("); }
                                                
                                            SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Link is now frozen! >=( " +
                                            "Health remaining: {4}{5} heart{6}. {0} now has {7} Mp16point{8} remaining! =)", e.ChatMessage.Username, pointsRequired,
                                            pointsRequired == 1 ? "" : "s", selectedCode.Name, newHealth/16, newHealthStr, newHealth/16 == 1 && newHealthStr == "" ? "" : "s",
                                            pointsRemain, pointsRemain == 1 ? "" : "s")); }
                                        }
                                           
                                        else if (selectedCode.Name.Equals("Link Freeze Burned")) {
                                            dynamic NewDataValueRead = new byte[1];
                                            //int curStateFreeze = ReadCode(NewDataValueRead, 1, 0x1DAB41); //If Link is Frozen in Animation address
                                            int curStateBurn = ReadCode(NewDataValueRead, 1, 0x1DB480); //If Link is on Fire
                                            int curStateFreeze = ReadCode(NewDataValueRead, 1, 0x406ED4); //If Link is Frozen in Ice address
                                            //int curStateLocked = ReadCode(NewDataValueRead, 1, 0x1DB09C); //If Camera is Locked

                                            //if (curStateLocked == 32 || curStateFreeze > 0 || curStateBurn == 1) { codePassed = false;
                                            if (curStateFreeze > 0 || curStateBurn == 1) { codePassed = false;
                                            SendMessage(channel, string.Format("{0} tried to use a freeze burn attack >=( but Link is currently in a state where he cannot be freeze burned! " +
                                            "No Mp16points were used, please try another command! =)", e.ChatMessage.Username)); }

                                            else { 
                                                int curHealth = ReadCode(NewDataValueRead, 1, 0x11A601); int newHealth = 0; //Current Health value
                                                if (curHealth <= 8) { newHealth = 0; } else { newHealth = curHealth - 8; }
                                                string newHealthStr = NewHealthFraction(newHealth);

                                            //WriteCode((byte)1, selectedCode.DataSize, 0x1DB480); // Link Frozen in Animation
                                            WriteCode((byte)1, selectedCode.DataSize, 0x406ED4);  // Link in Ice
                                            WriteCode((byte)newHealth, selectedCode.DataSize, 0x11A601);
                                            for (int i=0; i < 20; i++) { WriteCode((byte)255, selectedCode.DataSize, 0x1DB480 + (byte)i); }
                                            //WriteCode((byte)128, selectedCode.DataSize, selectedCode.MemoryOffset);
                                            
                                            //try { freezeThreadEnabled = true; freezeTimer = 60; freezerBurned = true;
                                            //    freezeSetThread = new Thread(IsLinkFrozen); freezeSetThread.Start(); }
                                            //catch { } // SendMessage(channel, "freeze failed =("); }
                                                
                                            SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Link is now freeze burned! >=( " +
                                            "Health remaining: {4}{5} heart{6} but that will drop even more soon. {0} now has {7} Mp16point{8} remaining! =)", e.ChatMessage.Username, pointsRequired,
                                            pointsRequired == 1 ? "" : "s", selectedCode.Name, newHealth/16, newHealthStr, newHealth/16 == 1 && newHealthStr == "" ? "" : "s",
                                            pointsRemain, pointsRemain == 1 ? "" : "s")); }
                                        }

                                        else if (selectedCode.Name.Equals("Current Time")) {

                                                dynamic NewDataValueRead = BitDonationManager.PointsToModifierPower(0, selectedCode.MinValue, selectedCode.MaxValue);
                                                var ReturnedTupleCheck = BitDonationManager.GetRequestedCodeCheck(userCommand);
                                                CodeInfo selectedCodeCheck = ReturnedTupleCheck.Item1;

                                                string curEnt1 = ReadCode(NewDataValueRead, 1, 0x11A5D2).ToString("X");
                                                string curEnt2 = ReadCode(NewDataValueRead, 1, 0x11A5D3).ToString("X");
                                                string curEnt = curEnt1 + curEnt2; //formatting issue

                                                if (curEnt == "0CD" || curEnt == "0EA" || curEnt == "12" || curEnt == "117" || curEnt == "123" || curEnt == "138" || curEnt == "139" ||
                                                    curEnt == "13D" || curEnt == "17D" || curEnt.StartsWith("18") || curEnt.StartsWith("1E") || curEnt.StartsWith("1F") || 
                                                    curEnt == "199" || curEnt == "19D" || curEnt == "1A5" || curEnt == "1B9" || curEnt == "1BD" || curEnt == "1D9" || curEnt == "1DD" ||
                                                    curEnt == "1F9" || curEnt == "1FD" || curEnt == "219" || curEnt == "21D" || curEnt == "229" || curEnt == "22D" || curEnt == "23D" ||
                                                    curEnt == "23E" || curEnt == "241" || curEnt == "242" || curEnt == "27A" || curEnt == "27E" || curEnt == "282" || curEnt == "28A" ||
                                                    curEnt == "28E" || curEnt == "292" || curEnt == "39" || curEnt == "311" || curEnt == "340" || curEnt == "341" || curEnt == "344" ||
                                                    curEnt == "3C8" || curEnt == "3CC" || curEnt == "3D0" || curEnt == "45B" || curEnt == "45F" || curEnt == "476" || curEnt == "47A" ||
                                                    curEnt == "47E" || curEnt == "47F" || curEnt == "4E6" || curEnt == "4FA" || curEnt == "4FB" || curEnt == "4FE" || curEnt == "5F" ||
                                                    curEnt == "560" || curEnt == "594" || curEnt == "64" || curEnt == "6C") { 

                                                    int curTime = ReadCode(NewDataValueRead, 1, selectedCode.MemoryOffset);
                                                    int curTime2 = ReadCode(NewDataValueRead, 1, selectedCode.MemoryOffset+0x01);
                                                    //int curTimeOfDay = ReadCode(NewDataValueRead, 1, 0x11A5E3);
                                                    int newTime = (curTime + 32) % 256; int newHour = (newTime * 24 / 256); int fwdTime = (newTime - curTime)*3/32; ;

                                                    if (curTime > 224 && newTime < 32) { newTime = 0; newHour = 0; fwdTime = (255 - curTime)*3/32; }
                                                    WriteCode((byte)newTime, selectedCode.DataSize, selectedCode.MemoryOffset);

                                                    if (newTime == 0) {
                                                    SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Time moved forward to " +
                                                    "a bit past midnight. {0} now has {4} Mp16point{5} remaining! =)", e.ChatMessage.Username, pointsRequired,
                                                    pointsRequired == 1 ? "" : "s", selectedCode.Name, pointsRemain, pointsRemain == 1 ? "" : "s")); }
                                                    else { 
                                                    SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Time moved forward by " +
                                                    "{4} hour{5} and is now a bit past {6}:00. {0} now has {7} Mp16point{8} remaining! =)", e.ChatMessage.Username, pointsRequired,
                                                    pointsRequired == 1 ? "" : "s", selectedCode.Name, fwdTime, fwdTime == 1 ? "" : "s", newHour, pointsRemain, 
                                                    pointsRemain == 1 ? "" : "s")); }
                                                    }

                                                else { codePassed = false;
                                                SendMessage(channel, string.Format("{0} tried to move time forward but BrianMp16 is in an area where time doesn't pass. " +
                                                "No Mp16points were used, please try another command! =)", e.ChatMessage.Username));
                                                }
                                            }

                                            else if (selectedCode.Name.Equals("Tunic Color")) {

                                                if (user2ndCommand == "" || user2ndCommand == "random" || user2ndCommand == "red" || user2ndCommand == "blue" || 
                                                    user2ndCommand == "green" || user2ndCommand == "yellow" || user2ndCommand == "orange" ||
                                                    user2ndCommand == "purple" || user2ndCommand == "pink" || user2ndCommand == "kokiri" ||
                                                    user2ndCommand == "goron" || user2ndCommand == "zora" || user2ndCommand == "white" ||
                                                    user2ndCommand == "silver" || user2ndCommand == "black" || user2ndCommand == "cyan" ||
                                                    user2ndCommand == "magenta" || user2ndCommand == "gray" || user2ndCommand == "gold" ||
                                                    user2ndCommand == "brown" || user2ndCommand == "lime" || user2ndCommand == "aqua") {

                                                dynamic NewDataValueRead = BitDonationManager.PointsToModifierPower(0, selectedCode.MinValue, selectedCode.MaxValue);
                                                int BytesRead = ReadCode(NewDataValueRead, 1, 0x11A640); string TunicType;
                                                if (BytesRead == 17) { TunicType = "Kokiri"; } else if (BytesRead == 18) { TunicType = "Goron"; }
                                                else if (BytesRead == 19) { TunicType = "Zora"; } else { TunicType = "other"; }
                                                string MessageMsg = string.Format("{0} used {1} Mp16point{2}! Code Ran: {3} Tunic Color | Value set to: {4} ",
                                                    e.ChatMessage.Username, pointsRequired, pointsRequired == 1 ? "" : "s", TunicType, user2ndCommand);

                                                byte[] TunicColor = new byte[3];
                                                if (user2ndCommand == "red") { TunicColor[0] = 0xFF; TunicColor[1] = 0; TunicColor[2] = 0; }
                                                else if (user2ndCommand == "blue") { TunicColor[0] = 0; TunicColor[1] = 0; TunicColor[2] = 0xFF; }
                                                else if (user2ndCommand == "green") { TunicColor[0] = 0; TunicColor[1] = 0x80; TunicColor[2] = 0; }
                                                else if (user2ndCommand == "yellow") { TunicColor[0] = 255; TunicColor[1] = 255; TunicColor[2] = 0; }
                                                else if (user2ndCommand == "orange") { TunicColor[0] = 255; TunicColor[1] = 165; TunicColor[2] = 0; }
                                                else if (user2ndCommand == "purple") { TunicColor[0] = 104; TunicColor[1] = 34; TunicColor[2] = 139; }
                                                else if (user2ndCommand == "pink") { TunicColor[0] = 255; TunicColor[1] = 192; TunicColor[2] = 203; }
                                                else if (user2ndCommand == "kokiri") { TunicColor[0] = 30; TunicColor[1] = 105; TunicColor[2] = 27; }
                                                else if (user2ndCommand == "goron") { TunicColor[0] = 100; TunicColor[1] = 20; TunicColor[2] = 0; }
                                                else if (user2ndCommand == "zora") { TunicColor[0] = 0; TunicColor[1] = 60; TunicColor[2] = 100; }
                                                else if (user2ndCommand == "white") { TunicColor[0] = 255; TunicColor[1] = 255; TunicColor[2] = 255; }
                                                else if (user2ndCommand == "silver") { TunicColor[0] = 192; TunicColor[1] = 192; TunicColor[2] = 192; }
                                                else if (user2ndCommand == "black") { TunicColor[0] = 0; TunicColor[1] = 0; TunicColor[2] = 0; }
                                                else if (user2ndCommand == "cyan") { TunicColor[0] = 0; TunicColor[1] = 255; TunicColor[2] = 255; }
                                                else if (user2ndCommand == "magenta") { TunicColor[0] = 255; TunicColor[1] = 0; TunicColor[2] = 255; }
                                                else if (user2ndCommand == "gray") { TunicColor[0] = 128; TunicColor[1] = 128; TunicColor[2] = 128; }
                                                else if (user2ndCommand == "gold") { TunicColor[0] = 255; TunicColor[1] = 215; TunicColor[2] = 0; }
                                                else if (user2ndCommand == "brown") { TunicColor[0] = 139; TunicColor[1] = 69; TunicColor[2] = 19; }
                                                else if (user2ndCommand == "lime") { TunicColor[0] = 0; TunicColor[1] = 255; TunicColor[2] = 0; }
                                                else if (user2ndCommand == "aqua") { TunicColor[0] = 0; TunicColor[1] = 255; TunicColor[2] = 255; }

                                                else { 
                                                    for (int i = 0; i < 3; i++) {
                                                        dynamic NewDataValue = BitDonationManager.PointsToModifierPower(0, selectedCode.MinValue, selectedCode.MaxValue);
                                                        TunicColor[i] = (byte)NewDataValue;
                                                        MessageMsg += (i == 0 ? "R" : (i == 1 ? "G" : "B")) + ": " + NewDataValue + " "; }
                                                }

                                                for (int i = 0; i < 3; i++) {
                                                    if (TunicType == "Kokiri") { KokiriTunicColor[i] = TunicColor[i]; }
                                                    else if (TunicType == "Goron") { GoronTunicColor[i] = TunicColor[i]; }
                                                    else if (TunicType == "Zora") { ZoraTunicColor[i] = TunicColor[i]; }
                                                    else { KokiriTunicColor[i] = TunicColor[i]; } }
                                                    MessageMsg += string.Format("| {0} now has {1} Mp16point{2} remaining! =)", e.ChatMessage.Username, pointsRemain,
                                                        pointsRemain == 1 ? "" : "s");
                                                SendMessage(channel, MessageMsg); } 

                                                else { codePassed = false;
                                                    SendMessage(channel, string.Format("{0} is not an available tunic color option. No Mp16points were used, " +
                                                        "please try again! =)", user2ndCommand));
                                                }
                                            }

                                            else if (selectedCode.Name.Equals("Ocarina Sound")) {
                                                dynamic NewDataValue = BitDonationManager.PointsToModifierPower(0, selectedCode.MinValue, selectedCode.MaxValue);
                                                OcarinaSound = new byte[1] { NewDataValue }; string ocarinaSFX = "";
                                                //int ocarinaSound = OcarinaSound[0];

                                               switch (OcarinaSound[0]) {
                                                    case 1: ocarinaSFX = "Ocarina"; break;
                                                    case 2: ocarinaSFX = "Malon's Voice"; break;
                                                    case 3: ocarinaSFX = "Impa's Whistle"; break;
                                                    case 4: ocarinaSFX = "Sheik's Harp"; break;
                                                    case 5: ocarinaSFX = "Crankbox Instrument"; break;
                                                    case 6: ocarinaSFX = "Skull Kid's Flute"; break;
                                                    //case 7: ocarinaSFX = "Slightly higher pitched Ocarina"; break;
                                                    default: ocarinaSFX = "Ocarina"; break; }
                                                
                                                WriteCode(OcarinaSound, 1, 0x10220C);
                                                SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Value set to: " +
                                                "{4}. {0} now has {5} Mp16point{6} remaining! =)", e.ChatMessage.Username, pointsRequired, 
                                                pointsRequired == 1 ? "" : "s", selectedCode.Name, ocarinaSFX, pointsRemain,
                                                pointsRemain == 1 ? "" : "s")); } //NewDataValue.ToString("X") for hex value

                                             else if (selectedCode.Name.Equals("Make It Rain")) {
                                                //dynamic NewDataValue = BitDonationManager.PointsToModifierPower(0, selectedCode.MinValue, selectedCode.MaxValue);
                                                //OcarinaSound = new byte[1] { NewDataValue }; string ocarinaSFX = "";
                                                //int ocarinaSound = OcarinaSound[0];

                                                WriteCode(selectedCode.MaxValue, selectedCode.DataSize, selectedCode.MemoryOffset);
                                                WriteCode(selectedCode.MinValue, selectedCode.DataSize, selectedCode.MemoryOffset + 0x03);
                                                SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Value set to: " +
                                                "Rain. {0} now has {4} Mp16point{5} remaining! =)", e.ChatMessage.Username, pointsRequired, 
                                                pointsRequired == 1 ? "" : "s", selectedCode.Name, pointsRemain, pointsRemain == 1 ? "" : "s")); }

                                            /*else if (selectedCode.Name.Equals("Link Thaw")) {
                                                if (e.ChatMessage.Username == "BrianMp16") { 
                                                dynamic NewDataValue = BitDonationManager.PointsToModifierPower(0, selectedCode.MinValue, selectedCode.MaxValue);
                                                WriteCode((byte)0, selectedCode.DataSize, selectedCode.MemoryOffset); }

                                                else { codePassed = false;
                                                SendMessage(channel, string.Format("A code whose command is thaw was not found. No Mp16points were used, " +
                                                "please try again! =)", userCommand)); } }*/

                                            else { //Item Amount case

                                                if (user2ndCommand.Length < 4) {
                                                    if (int.TryParse(user2ndCommand, out int n)) {
                                                            if (Convert.ToInt32(user2ndCommand) < 0) {
                                                                SendMessage(channel, string.Format("{0} tried to input a negative value. No, Cuyler, that is not allowed.", e.ChatMessage.Username));
                                                                }
                                                            else {

                                                            //bool magicUp = false; bool rupeeUp = false; //Used when an improper magic/rupee value is provided
                                                            //int newMagicInput = 0; int newRupeeInput = 0;

                                                            //if (selectedCode.CommandName == "magic" && Convert.ToInt32(user2ndCommand) % 2 != 0) {
                                                            //SendMessage(channel, string.Format("{0} tried to give {1} magic point{2} but magic can only be " +
                                                            //"given in even increments! No Mp16points were used, please try another command! =)", 
                                                            //e.ChatMessage.Username, user2ndCommand, user2ndCommand == "1" ? "" : "s")); }
                                                            //magicUp = true; newMagicInput = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(Convert.ToInt32(user2ndCommand))/2)); }
                                                            //else if (selectedCode.CommandName == "rupees" && Convert.ToInt32(user2ndCommand) % 3 != 0) {
                                                            //SendMessage(channel, string.Format("{0} tried to give {1} rupee{2} but rupees can only be " +
                                                            //"given in increments of 3! No Mp16points were used, please try another command! =)", 
                                                            //e.ChatMessage.Username, user2ndCommand, user2ndCommand == "1" ? "" : "s")); }
                                                            //rupeeUp = true; newRupeeInput = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(Convert.ToInt32(user2ndCommand)) / 3));
                                                            //}
                                                            //else { 

                                                            dynamic NewDataValueRead = BitDonationManager.PointsToModifierPower(0, selectedCode.MinValue, selectedCode.MaxValue);
                                                    var ReturnedTupleCheck = BitDonationManager.GetRequestedCodeCheck(userCommand);
                                                    CodeInfo selectedCodeCheck = ReturnedTupleCheck.Item1;

                                                    int currentItemAmount = ReadCode(NewDataValueRead, 1, selectedCode.MemoryOffset);
                                                    int bytesRead = ReadCode(NewDataValueRead, 1, selectedCodeCheck.MemoryOffset);
                                                    int maxItemAmount = Convert.ToInt32(selectedCodeCheck.MaxValue);
                                                    int curHealthFrac = currentItemAmount % 16; string curHealthStr = ""; //For use with hearts only
                                                    if (selectedCode.CommandName == "hearts") { maxItemAmount = bytesRead; }
                                                    if (selectedCode.CommandName == "magic") { int doubleMagic = ReadCode(NewDataValueRead, 1, 0x11A602); if (doubleMagic == 2) { maxItemAmount *= 2; } }
                                                    if (selectedCode.CommandName == "rupees") { bool bLeft = (bytesRead & (1 << 5)) != 0; bool bRight = (bytesRead & (1 << 4)) != 0;
                                                        int bitWalletLeft = bLeft ? 1 : 0; int bitWalletRight = bRight ? 1 : 0;
                                                        if (bitWalletLeft == 0 && bitWalletRight == 0) { maxItemAmount = 99; } else if (bitWalletLeft == 0 && bitWalletRight == 1) { maxItemAmount = 200; }
                                                        else if (bitWalletLeft == 1 && bitWalletRight == 0) { maxItemAmount = 500; } else { maxItemAmount = 999; }
                                                        currentItemAmount = currentItemAmount*256 + ReadCode(NewDataValueRead, 1, selectedCode.MemoryOffset+0x01); }
                                                      
                                                    if (bytesRead == Convert.ToInt32(selectedCodeCheck.MinValue) || selectedCode.CommandName == "hearts" || selectedCode.CommandName == "rupees") {

                                                        if (user2ndCommand == "0") {
                                                            if (currentItemAmount > 0) {
                                                                Random rnd = new Random(); int rndTakeRoll = 0;
                                                                if (selectedCode.CommandName == "hearts") { rndTakeRoll = rnd.Next(0, 1001); } //3% chance to drop to 0. 6% by half.
                                                                if (selectedCode.CommandName == "rupees") { rndTakeRoll = rnd.Next(0, 201); } //5% chance to drop to 0. 10% by half
                                                                else { rndTakeRoll = rnd.Next(0, 101); } //10% chance to drop to 0. 20% chance to reduce by half

                                                                if (rndTakeRoll <= 10) {
                                                                WriteCode((byte)0, selectedCode.DataSize, selectedCode.MemoryOffset);
                                                                if (selectedCode.CommandName == "rupees") { WriteCode((byte)0, selectedCode.DataSize, selectedCode.MemoryOffset+0x01); }
                                                                SendMessage(channel, string.Format("{0} tried to take all of Link's {1} and succeeded! >=( NOOOO! {0} used {2} Mp16point{3}! " +
                                                                "Code ran: Take All {4} | Value decreased to 0. {0} now has {5} Mp16point{6} remaining!", e.ChatMessage.Username, 
                                                                selectedCode.CommandName, pointsRequired, pointsRequired == 1 ? "" : "s", selectedCode.Name, pointsRemain, 
                                                                pointsRemain == 1 ? "" : "s")); }

                                                                else if (rndTakeRoll <= 30) {
                                                                int newItemAmount = currentItemAmount / 2;
                                                                if (selectedCode.CommandName == "rupees") { WriteCode((byte)(newItemAmount / 256), selectedCode.DataSize, selectedCode.MemoryOffset);
                                                                WriteCode((byte)(newItemAmount % 256), selectedCode.DataSize, selectedCode.MemoryOffset+0x01); }
                                                                else { WriteCode((byte)newItemAmount, selectedCode.DataSize, selectedCode.MemoryOffset); }

                                                                if (selectedCode.CommandName == "hearts") { newItemAmount /= 16; //newAmount = currentItemAmount - currentItemAmount % 16 + Convert.ToInt32(user2ndCommand) * 16;
                                                                        if (newItemAmount != maxItemAmount / 16) { switch (curHealthFrac) {
                                                                        case 0: curHealthStr = ""; break; case 1: curHealthStr = " & 1/16"; break; case 2: curHealthStr = " & 1/8"; break;
                                                                        case 3: curHealthStr = " & 3/16"; break; case 4: curHealthStr = " & 1/4"; break; case 5: curHealthStr = " & 5/16"; break;
                                                                        case 6: curHealthStr = " & 3/8"; break; case 7: curHealthStr = " & 7/16"; break; case 8: curHealthStr = " & 1/2"; break;
                                                                        case 9: curHealthStr = " & 9/16"; break; case 10: curHealthStr = " & 5/8"; break; case 11: curHealthStr = " & 11/16"; break;
                                                                        case 12: curHealthStr = " & 3/4"; break; case 13: curHealthStr = " & 13/16"; break; case 14: curHealthStr = " & 7/8"; break;
                                                                        case 15: curHealthStr = " & 15/16"; break; default: curHealthStr = ""; break;  } } }
                                                                    else if (selectedCode.CommandName == "magic") { newItemAmount /= 2; }
                                                                
                                                                SendMessage(channel, string.Format("{0} tried to take all of Link's {1} and half succeeded! >=( {0} used {2} Mp16point{3}! " +
                                                                "Code ran: Take Half of Link's Current {4} | Value decreased to {5}{6}. {0} now has {7} Mp16point{8} remaining!", e.ChatMessage.Username, 
                                                                selectedCode.CommandName, pointsRequired, pointsRequired == 1 ? "" : "s", selectedCode.Name, newItemAmount, curHealthStr,
                                                                pointsRemain, pointsRemain == 1 ? "" : "s")); }

                                                                else {
                                                                SendMessage(channel, string.Format("{0} tried to take all of Link's {1} and failed! >=) NICE TRY! {0} used {2} Mp16point{3}! " +
                                                                "Code ran: Take All {4} | Value remains unchanged. {0} now has {5} Mp16point{6} remaining!", e.ChatMessage.Username, 
                                                                selectedCode.CommandName, pointsRequired, pointsRequired == 1 ? "" : "s", selectedCode.Name, pointsRemain, 
                                                                pointsRemain == 1 ? "" : "s")); }   
                                                            }
                                                            else { codePassed = false;
                                                            SendMessage(channel, string.Format("{0} tried to take all of Link's {1} >=( but BrianMp16 is already out of {1}! No Mp16points were used, " +
                                                            "please try another command! =)", e.ChatMessage.Username, selectedCode.CommandName));
                                                            }
                                                        }

                                                        else { 
                                                        if (currentItemAmount < maxItemAmount) {

                                                            int pointsNeeded = selectedCodeCheck.MinimumPointDonation / 1000 + 
                                                               (selectedCodeCheck.MinimumPointDonation % 1000) * (Convert.ToInt32(user2ndCommand) - 1);
                                                            if (selectedCode.CommandName == "magic") { pointsNeeded /= 2; if (Convert.ToInt32(user2ndCommand) % 2 > 0) { pointsNeeded++; } }
                                                            else if (selectedCode.CommandName == "rupees") { pointsNeeded /= 3; if (Convert.ToInt32(user2ndCommand) % 3 > 0) { pointsNeeded++; } }

                                                            if (SQLdtPoints >= pointsNeeded) { int newAmount = 0;

                                                            if (selectedCode.CommandName == "hearts") { newAmount = currentItemAmount - currentItemAmount % 16 + Convert.ToInt32(user2ndCommand) * 16; }
                                                            else if (selectedCode.CommandName == "magic") { newAmount = currentItemAmount + Convert.ToInt32(user2ndCommand) * 2; }
                                                            else { newAmount = currentItemAmount + Convert.ToInt32(user2ndCommand); }

                                                                if (newAmount > maxItemAmount) {

                                                                if (selectedCode.CommandName == "hearts") { currentItemAmount /= 16; newAmount /= 16; maxItemAmount /= 16; }
                                                                else if (selectedCode.CommandName == "magic") { currentItemAmount /= 2; newAmount /= 2; maxItemAmount /= 2; }

                                                                pointsNeeded = selectedCodeCheck.MinimumPointDonation / 1000 +
                                                                (selectedCodeCheck.MinimumPointDonation % 1000) * (maxItemAmount - currentItemAmount - 1);
                                                                if (selectedCode.CommandName == "magic" && Convert.ToInt32(user2ndCommand) % 2 > 0) { pointsNeeded++; }
                                                                if (selectedCode.CommandName == "rupees") { pointsNeeded /= 3; if (Convert.ToInt32(user2ndCommand) % 3 > 0) { pointsNeeded++; } }
                                                                pointsRemain = pointsRemain + pointsRequired - pointsNeeded; pointsRequired = pointsNeeded;

                                                                int actualItemAmountProvided = Convert.ToInt32(user2ndCommand) - newAmount + maxItemAmount;
                                                                if (selectedCode.CommandName == "hearts") { newAmount = maxItemAmount * 16; }
                                                                else if (selectedCode.CommandName == "magic") { newAmount = maxItemAmount * 2; }
                                                                else { newAmount = maxItemAmount; }
                                                                WriteCode((byte)newAmount, selectedCode.DataSize, selectedCode.MemoryOffset);
                                                                if (selectedCode.CommandName == "rupees") { WriteCode((byte)(newAmount / 256), selectedCode.DataSize, selectedCode.MemoryOffset);
                                                                WriteCode((byte)(newAmount % 256), selectedCode.DataSize, selectedCode.MemoryOffset+0x01); }
                                                                //if (selectedCode.CommandName =="magic") { actualItemAmountProvided = Convert.ToInt32(user2ndCommand) - newAmount + maxItemAmount; }

                                                                SendMessage(channel, string.Format("{0} tried to give {1} {2}{3} but BrianMp16 can only hold {4} more! " +
                                                                "{0}'s {2} gift as well as total Mp16points used have been adjusted down accordingly. " +
                                                                "{0} used {5} Mp16point{6}! Code ran: {7} | Value increased by {4} to: {8}. " +
                                                                "{0} now has {9} Mp16point{10} remaining! =)", e.ChatMessage.Username, user2ndCommand,
                                                                selectedCodeCheck.CommandName, selectedCode.CommandName != "magic" ? "" : " points", actualItemAmountProvided,
                                                                pointsNeeded, pointsNeeded == 1 ? "" : "s", selectedCode.Name, maxItemAmount, pointsRemain, pointsRemain == 1 ? "" : "s")); 
                                                                }

                                                                else { 

                                                                Random rnd = new Random(); int rndMaxRoll = 0;
                                                                if (selectedCode.CommandName == "magic") { rndMaxRoll = rnd.Next(0, 201); } //1% per itemAmount given
                                                                else if (selectedCode.CommandName == "rupees") { rndMaxRoll = rnd.Next(0, 1001); } //0.2% per itemAmount given
                                                                else { rndMaxRoll = rnd.Next(0, 101); } //2% chance per itemAmount given.
                                                                pointsRemain = pointsRemain + pointsRequired - pointsNeeded; pointsRequired = pointsNeeded;

                                                                    if (rndMaxRoll > 2 * Convert.ToInt32(user2ndCommand)) {

                                                                    if (selectedCode.CommandName == "hearts") { newAmount = currentItemAmount + Convert.ToInt32(user2ndCommand) * 16; }
                                                                    else if (selectedCode.CommandName == "magic") { newAmount = currentItemAmount + Convert.ToInt32(user2ndCommand) * 2; }
                                                                
                                                                    //pointsRemain = pointsRemain + pointsRequired - pointsNeeded; pointsRequired = pointsNeeded;
                                                                    WriteCode((byte)newAmount, selectedCode.DataSize, selectedCode.MemoryOffset);
                                                                    if (selectedCode.CommandName == "rupees") { WriteCode((byte)(newAmount / 256), selectedCode.DataSize, selectedCode.MemoryOffset);
                                                                    WriteCode((byte)(newAmount % 256), selectedCode.DataSize, selectedCode.MemoryOffset+0x01); }

                                                                    if (selectedCode.CommandName == "hearts") { newAmount /= 16; //newAmount = currentItemAmount - currentItemAmount % 16 + Convert.ToInt32(user2ndCommand) * 16;
                                                                        if (newAmount != maxItemAmount / 16) { switch (curHealthFrac) {
                                                                        case 0: curHealthStr = ""; break; case 1: curHealthStr = " & 1/16"; break; case 2: curHealthStr = " & 1/8"; break;
                                                                        case 3: curHealthStr = " & 3/16"; break; case 4: curHealthStr = " & 1/4"; break; case 5: curHealthStr = " & 5/16"; break;
                                                                        case 6: curHealthStr = " & 3/8"; break; case 7: curHealthStr = " & 7/16"; break; case 8: curHealthStr = " & 1/2"; break;
                                                                        case 9: curHealthStr = " & 9/16"; break; case 10: curHealthStr = " & 5/8"; break; case 11: curHealthStr = " & 11/16"; break;
                                                                        case 12: curHealthStr = " & 3/4"; break; case 13: curHealthStr = " & 13/16"; break; case 14: curHealthStr = " & 7/8"; break;
                                                                        case 15: curHealthStr = " & 15/16"; break; default: curHealthStr = ""; break;  } } }
                                                                    else if (selectedCode.CommandName == "magic") { newAmount /= 2; }

                                                                    SendMessage(channel, string.Format("{0} used {1} Mp16point{2}! Code ran: {3} | Value increased by {4} to: " +
                                                                    "{5}{6}. {0} now has {7} Mp16point{8} remaining! =)", e.ChatMessage.Username, pointsRequired,
                                                                    pointsRequired == 1 ? "" : "s", selectedCode.Name, user2ndCommand, newAmount, curHealthStr, pointsRemain,
                                                                    pointsRemain == 1 ? "" : "s"));
                                                                    }

                                                                    else {
                                                                    WriteCode((byte)maxItemAmount, selectedCode.DataSize, selectedCode.MemoryOffset);
                                                                    if (selectedCode.CommandName == "rupees") { WriteCode((byte)(maxItemAmount / 256), selectedCode.DataSize, selectedCode.MemoryOffset);
                                                                    WriteCode((byte)(maxItemAmount % 256), selectedCode.DataSize, selectedCode.MemoryOffset+0x01); }

                                                                    if (selectedCode.CommandName == "hearts") { maxItemAmount /= 16; currentItemAmount /= 16; }
                                                                    else if (selectedCode.CommandName == "magic") { maxItemAmount /= 2; currentItemAmount /= 2; }

                                                                    SendMessage(channel, string.Format("{0} tried to give {1} {2}{3}, but hit the jackpot and " +
                                                                    "maxed them out instead!! =D {0} used {4} Mp16point{5}! Code ran: {6} | Value increased by {7} to: " +
                                                                    "{8}. {0} now has {9} Mp16point{10} remaining! =)", e.ChatMessage.Username, user2ndCommand, selectedCodeCheck.CommandName,
                                                                    selectedCode.CommandName != "magic" ? "" : " points", pointsRequired, pointsRequired == 1 ? "" : "s", selectedCode.Name, maxItemAmount - currentItemAmount, maxItemAmount, 
                                                                    pointsRemain, pointsRemain == 1 ? "" : "s")); }
                                                                }
                                                            }
                                                            else { codePassed = false;
                                                            SendMessage(channel, string.Format("{0} tried to give {1} {2}{3}, but {0} does not have enough Mp16points for that =( " +
                                                            "please acquire more or try a different command! =)", e.ChatMessage.Username, user2ndCommand, selectedCode.CommandName,
                                                            selectedCode.CommandName != "magic" ? "" : " points")); }

                                                        }
                                                        else { codePassed = false;
                                                            if (selectedCode.CommandName == "hearts") { maxItemAmount /= 16; }
                                                            else if (selectedCode.CommandName == "magic") { maxItemAmount /= 2; }
                                                            SendMessage(channel, string.Format("{0} tried to give {1}, but BrianMp16 already has a max of {2} {1}{3}! No Mp16points were used, " +
                                                            "please try another command! =)", e.ChatMessage.Username, selectedCode.CommandName, maxItemAmount,
                                                            selectedCode.CommandName != "magic" ? "" : " points")); }
                                                    }
                                                    }
                                                    else { codePassed = false;

                                                        if (userCommand != "0") { 
                                                        SendMessage(channel, string.Format("{0} tried to give {1}, but BrianMp16 {2}! No Mp16points were used, " +
                                                        "please try another command! =)", e.ChatMessage.Username, selectedCode.CommandName, selectedCodeCheck.Name)); }
                                                        else {
                                                        SendMessage(channel, string.Format("{0} tried to take all of Link's {1} >=( but BrianMp16 {2}! No Mp16points were used, " +
                                                        "please try another command! =)", e.ChatMessage.Username, selectedCode.CommandName, selectedCodeCheck.Name)); }
                                                        }
                                                    //}
                                                    } }
                                                    else { codePassed = false;
                                                        SendMessage(channel, string.Format("{0} tried to give {1}, but did not provide a value, " +
                                                        "please try again! =)", e.ChatMessage.Username, selectedCode.CommandName));
                                                    }
                                                }
                                                else { codePassed = false;
                                                    SendMessage(channel, string.Format("{0} tried to give {1}, but provided an invalid value, " +
                                                    "please try again! =)", e.ChatMessage.Username, selectedCode.CommandName));
                                                }
                                            }

                                        }

                                        if (codePassed == true) {
                                            //SendMessage(channel, "Your Mp16point request has been accepted and the code was run!");
                                            SQLdtPoints -= pointsRequired; SQLdt.Rows[row]["value"] = SQLdtPoints.ToString();
                                            using (SQLconn) {
                                                SQLconn.Open(); //Updates database with new point value for that user
                                                using (SQLiteCommand SQLcmdWrite = SQLconn.CreateCommand()) {
                                                    SQLcmdWrite.CommandText = "UPDATE phantombot_points SET value = @add3 WHERE variable = @add2";
                                                    SQLcmdWrite.Parameters.AddWithValue("@add2", e.ChatMessage.Username);
                                                    SQLcmdWrite.Parameters.AddWithValue("@add3", SQLdtPoints);
                                                    SQLcmdWrite.ExecuteNonQuery();
                                                } SQLconn.Close();
                                            }
                                        }

                                    }
                                    else { SendMessage(channel, "OoT Emulation not found! Oh no!"); }
                            }
                            else { SendMessage(channel, string.Format("{0} does not have enough Mp16points for that =( " +
                                    "please acquire more or try a different command! =)", e.ChatMessage.Username)); }
                        }
                    }
                }
                }
                }
            }
        }

        public string NewHealthFraction(int newHealth)
        {
            int newHealthFrac = newHealth % 16; string newHealthStr = "";
            switch (newHealthFrac)
            {
                case 0: newHealthStr = ""; break;
                case 1: newHealthStr = " & 1/16"; break;
                case 2: newHealthStr = " & 1/8"; break;
                case 3: newHealthStr = " & 3/16"; break;
                case 4: newHealthStr = " & 1/4"; break;
                case 5: newHealthStr = " & 5/16"; break;
                case 6: newHealthStr = " & 3/8"; break;
                case 7: newHealthStr = " & 7/16"; break;
                case 8: newHealthStr = " & 1/2"; break;
                case 9: newHealthStr = " & 9/16"; break;
                case 10: newHealthStr = " & 5/8"; break;
                case 11: newHealthStr = " & 11/16"; break;
                case 12: newHealthStr = " & 3/4"; break;
                case 13: newHealthStr = " & 13/16"; break;
                case 14: newHealthStr = " & 7/8"; break;
                case 15: newHealthStr = " & 15/16"; break;
                default: newHealthStr = ""; break;
            }
            return newHealthStr;
        }

        /*private void LoadPointsFromDB()
        {
            SQLiteConnection myConnection = new SQLiteConnection("Data Source=C:/Users/Brian/Desktop/OoT-Randomizer-master/" +
            "PhantomBot-2.4.2/PhantomBot-2.4.2/config/phantombot.db");
            myConnection.Open();
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.Connection = myConnection; cmd.CommandText = "Select * from phantombot_points";

            using (SQLiteDataReader sdr = cmd.ExecuteReader()) {
                DataTable dt = new DataTable();
                dt.Load(sdr); sdr.Close(); myConnection.Close();

                using (StreamWriter writer = new StreamWriter("c:/Users/Brian/Desktop/file.txt")) {
                    foreach (DataColumn col in dt.Columns) { }
                    foreach (DataRow row in dt.Rows) {
                        writer.WriteLine(row["section"].ToString());
                        writer.Write(" " + row["variable"].ToString());
                        writer.Write(" " + row["value"].ToString()); }
                    writer.Close();
                }
            }
        }*/

        private void SendWhisper(string Username, string Message)
        { client.SendWhisper(Username, Message); }

        private void SendMessage(JoinedChannel channel, string Message)
        { client.SendMessage(channel, Message); }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        { client.SendMessage(e.Channel, "Mp16Bot is live!"); }

        private void Client_OnConnect(object sender, OnJoinedChannelArgs e)
        { client.SendMessage(e.Channel, "Mp16Bot is live!"); }

        public bool enableConnectButton()
            => !string.IsNullOrEmpty(textBox1.Text) && !string.IsNullOrEmpty(textBox2.Text) && !string.IsNullOrEmpty(textBox3.Text);

        private void AuthTextBox_TextChanged(object sender, EventArgs e)
        { button1.Enabled = enableConnectButton(); }

        private void button4_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Keep in mind that you can always use a bot account to connect to your chat!", "OAuth Notice",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Process.Start("https://twitchapps.com/tmi/");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string ProcessName = "Project64";
            Process[] Processes = Process.GetProcessesByName(ProcessName);

            if (Processes.Length == 0) {
                ProcessName = "Project64d"; Processes = Process.GetProcessesByName(ProcessName); }
            if (Processes.Length > 0) {
                Process process = Processes[0]; processHandle = OpenProcess(0x1F0FFF, true, process.Id);
                baseAddress = ReadWritingMemory.GetBaseAddress(ProcessName, 0x10, 4096, 4);

                if (baseAddress != 0) {
                    SetStatus("Successfully hooked Project64 & found OoT's RAM Address");
                    try {
                        ConstantSetThreadEnabled = false;
                        if (ConstantSetThread != null) { while (ConstantSetThread.IsAlive) { } } // Wait for thread to finish execution
                        else { ConstantSetThread = new Thread(SetConstantValues); }

                        ConstantSetThreadEnabled = true;
                        ConstantSetThread.Start();
                    } catch { }
                }
                else { MessageBox.Show("Failed to find the RAM start address! Please try again at the title screen of OoT!", "Hook Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
            else { MessageBox.Show("Failed to find any Project64 processes! Please make sure that Project64 is running.", "Hook Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConstantSetThreadEnabled = false;

            if (client != null) {
                try { SendMessage(channel, "Mp16Bot has stopped!"); client.Disconnect(); } catch { SendMessage(channel, "Mp16Bot has stopped!"); }
            }
        }

    }
}
