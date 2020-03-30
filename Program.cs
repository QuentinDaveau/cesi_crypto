using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Code
{
    public delegate void KeyFoundCallback(byte[] key, int score);

    class Program
    {
        // Avec une clé de 6 caractères max alpha minuscules, on peut monter jusqu'à 300 millions de clés à tester (26 ^ 6)
        static void Main()
        {
            KeyGen KeyGen = new KeyGen(new int[]{-1, -1, -1, -1, -1, -1});

            string sampleFilePath = "crypted/PA.txt";
            Decryptor decryptor = new Decryptor(FileManager.GetFileBytes(sampleFilePath));

            string dictionaryPath = "dict/liste_francais.txt";
            string[][] dividedDict = DivideDict(File.ReadAllLines(dictionaryPath));

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            int batchLength = 100000;
            DictChecker dictChecker = new DictChecker(dividedDict, new KeyFoundCallback(ResultCallback));

            for (int i = 0; i < 1000; i++)
            {
                string[] batchCheck = new string[batchLength];
                byte[][] keyList = new byte[batchLength][];

                for (int j = 0; j < batchLength; j++)
                {
                    byte[] key = KeyGen.GenerageKey();
                    string tempString = Encoding.UTF8.GetString(decryptor.DecryptWithKey(key));
                    batchCheck[j] = tempString.Substring(0, (int)(tempString.Length / 2));
                    keyList[j] = key;
                }

                if (i % 10 == 0)
                {
                    Console.WriteLine("New checker! "+i+", key: "+KeyGen.GetKey());
                }
                
                Thread t = new Thread(dictChecker.CheckDecodedText);
                t.Start(new List<object>(){batchCheck, keyList});
            }

            stopWatch.Stop();
            Console.WriteLine(stopWatch.Elapsed);
        }

        private static string[][] DivideDict(string[] dict)
        {
            string letters = "abcdefghijklmnopqrstuvwxyz";
            string[][] dividedDict = new string[letters.Length * letters.Length][];

            for (int i = 0; i < letters.Length; i++)
            {
                for (int j = 0; j < letters.Length; j++)
                {
                    dividedDict[(i * 26) + j] = Array.FindAll(dict, x => x.StartsWith(letters[i] + "" + letters[j]));
                }
            }
            return dividedDict;
        }

        public static void ResultCallback(byte[] key, int score) 
        {
            // Console.WriteLine("Key: " + Encoding.UTF8.GetString(key) + ", score:" + score);
        }
    }

    
    class KeyGen
    {
        private static string keyCharacters = "abcdefghijklmnopqrstuvwxyz";

        // Pour maximiser les perfs on passe directement par un array (dirty) plutôt que faire un modulo ou autre calcul mathématique
        private int[] keyIncrement = new int[]{-1, -1, -1, -1, -1, -1};

        private string key = string.Empty;

        public KeyGen(int[] startValue)
        {
            keyIncrement = startValue;
        }

        public string GetKey()
        {
            string returnString = "";
            foreach(int value in keyIncrement) returnString += " "+value+",";
            return returnString;
        }

        public byte[] GenerageKey()
        {
            keyIncrement[0] += 1;

            if (keyIncrement[0] ==26)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (keyIncrement[i] == -1) continue;
                    if (keyIncrement[i] == 26)
                    {
                        keyIncrement[i] = 0;
                        keyIncrement[i + 1] += 1;
                    }
                }
            }

            key = string.Empty;

            foreach (int num in keyIncrement)
            {
                if (num == -1) continue;
                key += keyCharacters[num];
            }

            // Console.WriteLine(key);

            return Encoding.UTF8.GetBytes(key);
        }

        public void ResetKey()
        {
            keyIncrement = new int[]{0, -1, -1, -1, -1, -1};
        }
    }


    class FileManager
    {
        public static byte[] GetFileBytes(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return bytes;
        }

        public static string[] GetFileStrings(string path)
        {
            string[] lines = File.ReadAllLines(path);
            return lines;
        }
    }


    class Decryptor
    {
        private byte[] bytesToDecrypt;
        private int byteLength;
        public Decryptor(byte[] bytesToDecrypt)
        {
            this.bytesToDecrypt = bytesToDecrypt;
            byteLength = bytesToDecrypt.Length;
        }

        public byte[] DecryptWithKey(byte[] key)
        {
            byte[] result = new byte[byteLength];
            int keyLength = key.Length;

            for (int i = 0; i < byteLength; i++)
            {
                result[i] = (byte)(bytesToDecrypt[i] ^ key[i % keyLength]);
            }

            return result;
        }
    }


    class DictChecker
    {
        private string[][] dicts;
        private int threshold = 30;
        private KeyFoundCallback callback;
        private string letters = "abcdefghijklmnopqrstuvwxyz";

        public DictChecker(string[][] dicts, KeyFoundCallback callbackDelegate)
        {
            this.dicts = dicts;
            this.callback = callbackDelegate;
        }

        public void CheckDecodedText(object properties)
        {
            string[] texts = (string[])((List<object>)properties)[0];
            byte[][] keys = ((byte[][])((List<object>)properties)[1]);

            properties = null;

            for (int i = 0; i < texts.Length; i++)
            {
                int score = 0;
                foreach (string word in Regex.Replace(texts[i], "[^a-zA-Zéè ]", string.Empty).ToLower().Split(' '))
                {
                    if (word.Length < 2) continue;
                    if (Array.FindIndex(dicts[(letters.IndexOf(word[0]) * 26) + letters.IndexOf(word[1])], x => x == word) > -1)
                    {
                        score += word.Length;
                    }
                }

                texts[i] = null;

                if (score >= threshold)
                {
                    // callback(keys[i], count);
                    Console.WriteLine("Key: " + Encoding.UTF8.GetString(keys[i]) + ", score:" + score);
                }
                else
                {
                    keys[i] = null;
                }
            }
        }
    }
}