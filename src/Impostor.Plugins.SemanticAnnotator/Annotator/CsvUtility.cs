using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    public class CsvUtility
    {
        public static DateTimeOffset TimeStamp { get;  set; }
        public static void CsvGenerator(string gameId, string timeStamp, string player, string positionX, string positionY)
        {
            if(string.IsNullOrEmpty(player))
            {
                return;
            }
            // Define the file path
            string filePath = "movement_data_"+gameId+".csv";

            // Write or append data to the CSV file
            using (StreamWriter writer = System.IO.File.Exists(filePath) ? System.IO.File.AppendText(filePath) : new StreamWriter(filePath))
            {
                // If the file is newly created, write the headers
                if (writer.BaseStream.Position == 0)
                {
                    // Write column headers
                    writer.WriteLine("GameId;TimeStamp;Player;PositionX;PositionY");
                }

                // Write the rows with the provided values
                writer.WriteLine($"{gameId};{timeStamp};{player};{positionX};{positionY}");
            }

            // Confirm file update or creation
            //Console.WriteLine($"CSV file '{filePath}' has been updated or created.");
        }

        public static void CsvGeneratorStartGame(string gameId, string timeStamp)
        {
            // Define the file path
            string filePath = "movement_data_" + gameId + ".csv";

            // Write or append data to the CSV file
            using (StreamWriter writer = System.IO.File.Exists(filePath) ? System.IO.File.AppendText(filePath) : new StreamWriter(filePath))
            {
                // If the file is newly created, write the headers
                if (writer.BaseStream.Position == 0)
                {
                    // Write column headers
                    writer.WriteLine("GameId;TimeStamp;Player;PositionX;PositionY");
                }

                // Write the rows with the provided values
                writer.WriteLine($"{gameId};{timeStamp};START;;");
            }

            // Confirm file update or creation
            //Console.WriteLine($"CSV file '{filePath}' has been updated or created.");
        }

        public static void CsvGeneratorEndGame(string gameId, string timeStamp)
        {
            // Define the file path
            string filePath = "movement_data_" + gameId + ".csv";

            // Write or append data to the CSV file
            using (StreamWriter writer = System.IO.File.Exists(filePath) ? System.IO.File.AppendText(filePath) : new StreamWriter(filePath))
            {
                // If the file is newly created, write the headers
                if (writer.BaseStream.Position == 0)
                {
                    // Write column headers
                    writer.WriteLine("GameId;TimeStamp;Player;PositionX;PositionY");
                }

                // Write the rows with the provided values
                writer.WriteLine($"{gameId};{timeStamp};END;;");
            }

            // Confirm file update or creation
            //Console.WriteLine($"CSV file '{filePath}' has been updated or created.");
        }
    }
}
