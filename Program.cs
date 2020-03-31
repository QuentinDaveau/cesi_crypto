using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

/*
Code développé en suivant rigoureusement la méthode de la R.A.C.H.E. conformément à la norme ISO 1664:
https://www.la-rache.com/
*/

namespace Code
{
    public delegate void KeyFoundCallback(byte[] key, int score);

    // Programme principal

    class Program
    {
        // Avec une clé de 6 caractères max alpha minuscules, on peut monter jusqu'à 300 millions de clés à tester (26 ^ 6)
        // Fonction principale (appelée au démarrage)
        static void Main()
        {
            // Fonction de recherche de la clé
            SearchKey();

            // Bout de code pour décrypter les fichiers
            // foreach(char c in "ABCDEFGHIJK")
            // {
            //     FileDecrypt.DecryptFile("crypted/P"+c+".txt", "decrypted/P"+c+".txt", "buoxmh");
            // }
        }

        // Fonction principale de recherche de la clé
        private static void SearchKey()
        {
            // Création d'un KeyGen. On initialise le point de départ de génération de la clé à 0
            KeyGen KeyGen = new KeyGen(new int[]{-1, -1, -1, -1, -1, -1});

            // Récupération du texte contenu dans le premier fichier. Initialisation d'un Decryptor à partir du premier fichier.
            string sampleFilePath = "crypted/PA.txt";
            Decryptor decryptor = new Decryptor(FileManager.GetFileBytes(sampleFilePath));

            // Chargement du dictionnaire et division de celui-ci en sous-dictionnaires pour accélérer la vitesse de recherche
            string dictionaryPath = "dict/liste_francais.txt";
            string[][] dividedDict = DivideDict(File.ReadAllLines(dictionaryPath));

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // Création d'un DictChecker
            DictChecker dictChecker = new DictChecker(dividedDict, new KeyFoundCallback(ResultCallback));

            // Création de batchs et lancement de threads qui analyseront les batchs en question
            int batchLength = 100000;

            for (int i = 0; i < 1000; i++)
            {
                string[] batchCheck = new string[batchLength];
                byte[][] keyList = new byte[batchLength][];

                for (int j = 0; j < batchLength; j++)
                {
                    // Génération des textes décryptés et stockage dans un batch
                    byte[] key = KeyGen.GenerageKey();
                    string tempString = Encoding.UTF8.GetString(decryptor.DecryptWithKey(key));
                    batchCheck[j] = tempString.Substring(0, (int)(tempString.Length / 2));
                    keyList[j] = key;
                }

                if (i % 10 == 0)
                {
                    Console.WriteLine("New checker! "+i+", key: "+KeyGen.GetKey());
                }
                
                // Lancement d'un nouveau thread qui analysera le nouveau batch
                Thread t = new Thread(dictChecker.CheckDecodedText);
                t.Start(new List<object>(){batchCheck, keyList});
            }

            stopWatch.Stop();
            Console.WriteLine(stopWatch.Elapsed);
        }

        // Fonction de division du dictionnaire. Divise le dictionnaire en 26 * 26 dictionnaires contenant  chacun
        // tous les mots commencant par une combinaison unique de deux lettres de l'alphabet. A pour objectif de réduire
        // la quantité de mots à comparer en ne recherchant que dans le dictionnaire concerné
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

        // Fonction de callback pouvant être appelée par les threads
        public static void ResultCallback(byte[] key, int score) 
        {
            // Console.WriteLine("Key: " + Encoding.UTF8.GetString(key) + ", score:" + score);
        }
    }

    
    // Classe de génération des clés
    class KeyGen
    {
        private static string keyCharacters = "abcdefghijklmnopqrstuvwxyz";

        // KeyIncrement: compteur qui va définir la nouvelle clé à générer. Array de 6 int. Chaque int représente une lettre de l'alphabet (-1 = rien)
        // On incrémente à chaque fois le premier int. Si il vaut 26 (= z + 1), on le repasse à "a", et on incrémente le int suivant de 1.
        // {0, -1, -1, -1, -1, -1} = "a", {5, 2, 1, -1, -1, -1} = "fcb"...
        // Pour maximiser les perfs on passe directement par un array (dirty) plutôt que faire un modulo ou autre calcul mathématique.
        private int[] keyIncrement = new int[]{-1, -1, -1, -1, -1, -1};

        private string key = string.Empty;

        // A la création de la classe, il faut préciser la valeur de départ désitée
        public KeyGen(int[] startValue)
        {
            keyIncrement = startValue;
        }

        // Retourne la clé actuelle sous forme de string
        public string GetKey()
        {
            string returnString = "";
            foreach(int value in keyIncrement) returnString += " "+value+",";
            return returnString;
        }

        // Génère une nouvelle clé. Incrémente la liste de 1, convertis les int en lettre de l'alphabet et retourne une liste binaire (la clé)
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
    }


    // Classe dédiée au chargement des fichiers
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


    // Classe de décryption du texte (sous forme de bytes) à parir d'une clé. Elle applique un XOR.
    class Decryptor
    {
        private byte[] bytesToDecrypt;
        private int byteLength;

        // Définition du texte à décrypter à l'initialisation de la classe
        public Decryptor(byte[] bytesToDecrypt)
        {
            this.bytesToDecrypt = bytesToDecrypt;
            byteLength = bytesToDecrypt.Length;
        }

        // Décryptage du texte en utilisant la clé donnée.
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


    // Classe de comparaison du texte avec le dictionnaire afin de retourner un score de lisibilité
    class DictChecker
    {
        private string[][] dicts;
        private int threshold = 30;
        private KeyFoundCallback callback;
        private string letters = "abcdefghijklmnopqrstuvwxyz";

        // A l'initialisation, chargement des dictionnaires et de la fonction de retour (pour le multithreading)
        public DictChecker(string[][] dicts, KeyFoundCallback callbackDelegate)
        {
            this.dicts = dicts;
            this.callback = callbackDelegate;
        }

        // Comparaison d'un batch de texts avec le dictionnaire. Pour chaque text présent dans le batch,
        // récupération des mots présent dans le texte (toute chaîne de caractères encadrée d'espaces) et recherche de ceux-ci
        // dans le dictionnaire. Si le mot est trouvé, le score du texte augmente de la longeur du mot.
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

                // Nettoyage de l'entrée du texte qui vient d'être testé pour libérer de la mémoire
                texts[i] = null;

                // Si score suffisamment élevé
                if (score >= threshold)
                {
                    // Retourne les valeurs par le biais du callback, ou bien les affiche directement dans la console
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


    // Simple classe de décryptage des fichiers. Charge un fichier, le décrypte et enregistre le résultat au path donné
    class FileDecrypt
    {
        public static void DecryptFile(string filePath, string savePath, string key)
        {
            Decryptor decryptor = new Decryptor(FileManager.GetFileBytes(filePath));
            File.WriteAllBytes(savePath, decryptor.DecryptWithKey(Encoding.UTF8.GetBytes(key)));
        }
    }
}