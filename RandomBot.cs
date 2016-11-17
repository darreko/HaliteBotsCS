using System;
using System.Collections.Generic;
using System.Linq;

public class RandomBot
{
    public const string RandomBotName = "RandomCSharpBot";

    public static void Main(string[] args) {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);

        ushort myID;
        var map = Networking.getInit(out myID);

        Networking.SendInit(RandomBotName); 

        var random = new Random();
        while (true)
        {
            try
            {
                Networking.getFrame(ref map); // Update the map to reflect the moves before this turn
            }
            catch (Exception)
            {
                return;
            }

            var moves = new List<Move>();
            for (ushort x = 0; x < map.Width; x++) {
                for (ushort y = 0; y < map.Height; y++) {
                    if (map[x, y].Owner == myID) {
                        moves.Add(new Move {
                            Location = new Location {X = x, Y = y},
                            Direction = (Direction)random.Next(5)
                        });
                    }
                }
            }

            Networking.SendMoves(moves); // Send moves
        }
    }
}
