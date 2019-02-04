using System;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

namespace OoTBitRaceRandomizer
{
    public static class BitDonationManager
    {
        [DllImport("kernel32.dll", EntryPoint = "ReadProcessMemory", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int ReadProcessMemory1(int hProcess, int lpBaseAddress, ref int lpBuffer, int nSize, ref int lpNumberOfBytesRead);

        private static readonly Random Generator = new Random();
        private static readonly CodeInfo[] CodeInfoArray =
        {
            new CodeInfo("Current Time",            "time",         0x11A5DC,   2,   (byte)0,     (byte)0xFF), // 0xC000 is the start of night 0x0000 = Midnight? 0x4500 = start of day
            new CodeInfo("Tunic Color",             "tunic",        0x0F7AD8,   1,     (byte)0,         (byte)0xFF),
            new CodeInfo("Tunic Color Choose",      "tunic2",       0x0F7AD8,   3,     (byte)0,         (byte)0xFF),
            new CodeInfo("Ocarina Sound",           "ocarina",      0x10220C,   1,     (byte)1,         (byte)7), //8
            new CodeInfo("Current Arrows",          "arrows",       0x11A65F,   2,     (byte)0,         (byte)51),
            new CodeInfo("Current Bombs",           "bombs",        0x11A65E,   4,     (byte)0,         (byte)41),
            new CodeInfo("Current Bombchus",        "bombchus",     0x11A664,   5,     (byte)0,         (byte)51),
            new CodeInfo("Current Slingshot Ammo",  "seeds",        0x11A662,   2,     (byte)0,         (byte)31),
            new CodeInfo("Current Deku Nuts",       "nuts",         0x11A65D,   2,     (byte)0,         (byte)41),
            new CodeInfo("Current Deku Sticks",     "sticks",       0x11A65C,   3,     (byte)0,         (byte)31),
            new CodeInfo("Current Magic",           "magic",        0x11A603,   1,     (byte)0,         (byte)97),
            new CodeInfo("Current Rupees",          "rupees",       0x11A604,   1,     (byte)0,         (byte)0xFF),
            new CodeInfo("Current Hearts",          "hearts",       0x11A601,   4,     (byte)0,         (byte)0xFF),
            new CodeInfo("Current B Button Weapon", "weapon",       0x11A638,   5,     (byte)1,         (byte)0x3E),
            new CodeInfo("Current B Button Weapon", "weapon2",      0x11A638,   7,    (byte)1,         (byte)0x3E),
            new CodeInfo("Current Magic Beans",     "beans",        0x11A66A,   9,     (byte)0,         (byte)11),
            new CodeInfo("Link on Fire",            "burn",         0x1DB480,   5,     (byte)0,         (byte)11),
            //new CodeInfo("Link Frozen",             "freeze",       0x1DB0A2,   7,     (byte)0,         (byte)11),
            new CodeInfo("Link Frozen",             "freeze",       0x1DAB41,   7,     (byte)0,         (byte)11),
            new CodeInfo("Link Thaw",               "thaw",         0x1DAB41,   1,     (byte)0,         (byte)11),
            new CodeInfo("Link Freeze Burned",      "freezeburn",   0x1DAB41,   16,    (byte)0,         (byte)11),
            new CodeInfo("Link Freeze Burned",      "freezerburn",  0x1DAB41,   16,    (byte)0,         (byte)11),
            new CodeInfo("Make It Rain",            "rain",         0x1D8FB3,   1,    (byte)20,         (byte)128),
            // New Navi sound effect
            // Change music in an area
            // Low Health sound effect
            // Change Navi's color?
            //new CodeInfo("Chang Equipped Sword",    0x13F54D,   5,   (byte)0x3B,         (byte)0x3E), // 3B = Kokiri Sword, 3D = Biggorn Sword
            // Link Position: floats? 0x13F434, 0x13F438, 0x13F43C (X, Y, Z?)
            // 0x11A64C = Item Bitmap
        };

        private static readonly CodeInfo[] CodeInfoArrayCheck =
        {
            new CodeInfo("doesn't have a Fairy Bow yet",        "arrows",       0x11A647,   1001,     (int)3,           (int)30),
            new CodeInfo("doesn't have a Bomb Bag yet",         "bombs",        0x11A646,   4002,     (int)2,           (int)20),
            new CodeInfo("cannot currently hold Bombchus",      "bombchus",     0x11A64C,   5003,     (int)9,           (int)50),
            new CodeInfo("doesn't have a Slingshot yet",        "seeds",        0x11A64A,   1001,     (int)6,           (int)30),
            new CodeInfo("cannot currently hold Deku Nuts",     "nuts",         0x11A645,   2001,     (int)1,           (int)20),
            new CodeInfo("cannot currently hold Deku Sticks",   "sticks",       0x11A644,   3002,     (int)0,           (int)10),
            new CodeInfo("doesn't have a Magic Meter yet",      "magic",        0x11A60A,   1001,     (byte)1,          (byte)48),
            new CodeInfo("Double Magic?",                       "magic2",       0x11A602,   1001,     (byte)2,          (byte)96),
            new CodeInfo("Check Wallet",                        "rupees",       0x11A672,   1001,     (byte)0,          (byte)0),
            new CodeInfo("Health Null",                         "hearts",       0x11A5FF,   4002,     (byte)0,          (byte)0),
            new CodeInfo("Check Age",                           "weapon",       0x11A5D4,   0,        (byte)1,          (byte)0),
            new CodeInfo("cannot currently hold Magic Beans",   "beans",        0x11A652,   9005,     (byte)16,         (byte)10),
        };

        /// <summary>
        /// Returns the points required for a code for CodeInfoArray.
        /// <param name="Name">The command name of the code.</param>
        /// <returns>The minimum point donation required to run the code. If the code doesn't exist, -1 is returned.</returns>
        public static int GetPointsRequiredForCodeByCommandName(string Name) {
            for (int i = 0; i < CodeInfoArray.Length; i++) {
                if (CodeInfoArray[i].CommandName.Equals(Name)) {
                    return CodeInfoArray[i].MinimumPointDonation;
                }
            } return -1;
        }

       
        /// <summary>
        /// Returns a Randomized value between Min and Max, with the Minimum adjusted by the point amount requested.
        /// <param name="Points">How many points were requested</param>
        /// <param name="Min">The minimum value to be returned</param>
        /// <param name="Max">The maximum value to be returned</param>
        /// <returns>Random number between min and max, with the min adjusted by the point amount requested.</returns>
        public static T PointsToModifierPower<T>(int Points, T Min, T Max)
        {
            Type TType = Min.GetType();
            if (TType == Max.GetType())
            {
                try
                {
                    dynamic dMin = (dynamic)Min;
                    dynamic dMax = (dynamic)Max;
                    float PointPercentage = 1 / dMax;
                    dynamic AdjustedMinimum = (dynamic)Math.Min(dMax, Math.Max(dMin, (dynamic)Convert.ChangeType(PointPercentage * Points, TType)));
                    return (T)Convert.ChangeType(Generator.Next(AdjustedMinimum, dMax), TType);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
            return Min;
        }

        /// <summary>
        /// Returns a random CodeInfo object that is valid for the point requested amount supplied.
        /// </summary>
        /// <param name="PointAmount">The point requested amount</param>
        /// <returns>A random CodeInfo whose required point requested amount is less-than or equal-to the point requested amount.</returns>
        /*public static CodeInfo GetRandomCodeInfoForPointAmount(int PointAmount)
        {
            var ValidCodeInfos = CodeInfoArray.Where(c => c.MinimumPointDonation <= PointAmount);
            int InfoCount = ValidCodeInfos.Count();

            if (InfoCount < 1)
            {
                return null;
            }

            return ValidCodeInfos.ElementAt(Generator.Next(0, InfoCount));
        }*/

        /// <summary>
        /// Returns a code that matches a command string, or if no matches were found, null.
        /// <param name="Input">The command string</param>
        /// <param name="PointAmount">The amount of points requested</param>
        /// <returns>A tuple containing the selected CodeInfo (null if a failed code), and a success code.</returns>
        public static Tuple<CodeInfo, int> GetRequestedCode(string Input)//, int PointAmount)
        { Input = Input.ToLower(); 

            for (int i = 0; i < CodeInfoArray.Length; i++) {
                if (CodeInfoArray[i].CommandName.Equals(Input)) {
                    //if (CodeInfoArray[i].MinimumPointDonation <= PointAmount)
                    { return new Tuple<CodeInfo, int>(CodeInfoArray[i], 1); }
                    //else { return new Tuple<CodeInfo, int>(GetRandomCodeInfoForBitAmount(BitAmount), -1);
                       // return new Tuple<CodeInfo, int>(null, -1); }*/
                } }
            //return new Tuple<CodeInfo, int>(GetRandomCodeInfoForBitAmount(BitAmount), 0);
            return new Tuple<CodeInfo, int>(null, 0);
        }

        /// <summary>
        /// Returns a code that matches a command string, or if no matches were found, null.
        /// <param name="Input">The command string</param>
        /// <param name="PointAmount">The amount of points requested</param>
        /// <returns>A tuple containing the selected CodeInfoCheck (null if a failed code), and a success code.</returns>
        public static Tuple<CodeInfo, int> GetRequestedCodeCheck(string Input)//, int PointAmount)
        { Input = Input.ToLower(); 
            for (int i = 0; i < CodeInfoArrayCheck.Length; i++) {
                if (CodeInfoArrayCheck[i].CommandName.Equals(Input)) {
                    return new Tuple<CodeInfo, int>(CodeInfoArrayCheck[i], 1);
                } }

            return new Tuple<CodeInfo, int>(null, 0);
        }

 
    }
}
