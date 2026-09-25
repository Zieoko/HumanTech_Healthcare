using System;
using System.IO;
using System.IO.Ports;

class Program
{
    static void Main(string[] args)
    {
        // ⚠️ Remplace COM3 par ton port Arduino (COM4, COM5, etc.)
        SerialPort port = new SerialPort("COM3", 9600);
        port.Open();

        Console.WriteLine("Réception JSON depuis Arduino...");

        // Le fichier sera créé automatiquement s'il n'existe pas
        using (StreamWriter writer = new StreamWriter("data.json", true))
        {
            while (true)
            {
                string ligne = port.ReadLine();   // Lecture du JSON
                Console.WriteLine(ligne);         // Affichage dans la console
                writer.WriteLine(ligne);          // Écriture dans le fichier
                writer.Flush();                   // Sauvegarde immédiate
            }
        }
    }
}
