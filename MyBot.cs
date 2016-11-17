using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class MyBot
{
    public const string MyBotName = "DarrekBot";

    public static void Main(string[] args)
    {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);

        ushort myID;
        var map = Networking.getInit(out myID);
        const ushort MAX_STRENGTH = 255;
        const ushort HALF_STRENGTH = 128;

        /* ------
            Do more prep work, see rules for time limit
        ------ */

        Networking.SendInit(MyBotName); // Acknoweldge the init and begin the game

        var random = new Random();

        //var logFileName = string.Format(@"C:\Users\dolson\Source\Halite\Halite-C#-Starter-Package\Halite-C#-Starter-Package\log{0}.txt", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
        var logFileName = string.Format("log{0}.txt", DateTime.Now.Ticks);

        AppendLog(logFileName, "Starting battle!");
        int frameNumber = 0;

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

            frameNumber++;

            AppendLog(logFileName, string.Format("Frame {0}", frameNumber));

            //Try to attack in a straight line to achive maximum "overkill"
            var previousAttackDirectionByLocation = new Dictionary<Location, Direction>();
            
            var moves = new List<Move>();
            for (ushort x = 0; x < map.Width; x++)
            {
                for (ushort y = 0; y < map.Height; y++)
                {
                    if (map[x, y].Owner == myID)
                    {
                        var activeSite = new SiteEx(map, x, y);

                        var neighbors = new List<SiteEx>()
                        {
                            GetNeighbor(map, activeSite, Direction.West),
                            GetNeighbor(map, activeSite, Direction.East),
                            GetNeighbor(map, activeSite, Direction.North),
                            GetNeighbor(map, activeSite, Direction.South)
                        };

                        var friendlySites = neighbors.Where(c => c.Owner == myID);
                        var potentialLunchSites =
                            neighbors.Where(c => c.Strength < activeSite.Strength && c.Owner != myID);
                        var killSites = potentialLunchSites.Where(c => c.Owner != 0);
                        var dangerSites =
                            neighbors.Where(c => c.Strength > activeSite.Strength && c.Owner != myID && c.Owner != 0); //TODO: what to do with danger sites?
                        

                        //1. Continue an attack spearhead from previous turn first
                        if (previousAttackDirectionByLocation.ContainsKey(activeSite.Location))
                        {
                            var directionToMove = previousAttackDirectionByLocation[activeSite.Location];
                            previousAttackDirectionByLocation.Remove(activeSite.Location);

                            var neighbor = GetNeighbor(map, activeSite, directionToMove);
                            if (neighbor.Owner != myID && neighbor.Owner != 0)
                            {
                                previousAttackDirectionByLocation.Add(neighbor.Location, directionToMove);
                            }

                            moves.Add(new Move
                            {
                                Location = activeSite.Location,
                                Direction = directionToMove
                            });
                        }
                        //2. Try to kill anything in sight
                        else if (killSites.Any())
                        {
                            var killSite = killSites.First();
                            var directionToMove = GetMoveDirection(map, activeSite, killSite);
                            

                            //AppendLog(logFileName, string.Format("Attack from [{0},{1}] (strength {4}) to [{2},{3}] (strength {5})",
                            //    activeSite.X, activeSite.Y, killSite.X, killSite.Y, activeSite.Strength,
                            //    killSite.Strength));

                            previousAttackDirectionByLocation.Add(GetNeighbor(map, activeSite, directionToMove).Location, directionToMove);

                            moves.Add(new Move
                            {
                                Location = activeSite.Location,
                                Direction = directionToMove
                            });
                        }
                        //3. Try to take over neutral
                        else if (potentialLunchSites.Any())
                        {
                            var lunchSite = potentialLunchSites.OrderByDescending(s => s.Production).First();
                            var directionToMove = GetMoveDirection(map, activeSite, lunchSite);

                            //AppendLog(logFileName, string.Format(
                            //    "Consume Neutral from [{0},{1}] (strength {4}) to [{2},{3}] (strength {5})",
                            //    activeSite.X, activeSite.Y, lunchSite.X, lunchSite.Y, activeSite.Strength,
                            //    lunchSite.Strength));
                            

                            moves.Add(new Move
                            {
                                Location = activeSite.Location,
                                Direction = directionToMove
                            });
                        }
                        //3. Find a buddy to merge with
                        //else if (friendlySites.Any(c => c.Strength < activeSite.Strength && c.Strength + activeSite.Strength > 25) && activeSite.Strength > 10)
                        //{
                        //    var friendSite = friendlySites.FirstOrDefault(c => c.Strength < activeSite.Strength && c.Strength + activeSite.Strength > 25);
                        //    var directionToMove = GetMoveDirection(map, activeSite, friendSite);

                        //    moves.Add(new Move
                        //    {
                        //        Location = activeSite.Location,
                        //        Direction = directionToMove
                        //    });
                        //}




                        //4. If all neighbors are friendly, move towards nearest edge.
                        else if (friendlySites.Count() == neighbors.Count && ((activeSite.Strength >= (activeSite.Production * 5)) || activeSite.Strength > HALF_STRENGTH))
                        {
                            var nearestNonPlayerDirection = GetNearestNonPlayerDirection(map, activeSite, myID);
                            if (nearestNonPlayerDirection != Direction.Still)
                            {
                                //AppendLog(logFileName, string.Format("Move to edge from [{0},{1}] (strength {3}) to {2}",
                                //    activeSite.X, activeSite.Y, nearestNonPlayerDirection, activeSite.Strength));

                                moves.Add(new Move
                                {
                                    Location = activeSite.Location,
                                    Direction = nearestNonPlayerDirection
                                });
                            }
                            else //All orthagonal directions are occupied.
                            {
                                //TODO: could have logic here to find enemies or something, or go diagonally.
                            }
                        }

                        ////4. TODO: If large enough, move towards the closest neutral edge.
                        ////5. if all else fails, move randomly.
                        //else if (neighbors.Any(n => n.Strength < activeSite.Strength))
                        //{
                        //    moves.Add(new Move
                        //    {
                        //        Location = new Location { X = x, Y = y },
                        //        Direction = (Direction)random.Next(5)
                        //    });
                        //}




                    }
                }
            }

            Networking.SendMoves(moves); // Send moves

        }

    }

    private static void AppendLog(string logFileName, string line)
    {
        using (var sw = File.AppendText(logFileName))
        {
            sw.WriteLine(line);
        }
    }

    static SiteEx GetNeighbor(Map map, SiteEx from, Direction direction)
    {
        var leftCoord = (ushort)(from.X == 0 ? (map.Width - 1) : from.X - 1);
        var rightCoord = (ushort)(from.X == (map.Width - 1) ? 0 : from.X + 1);
        var upCoord = (ushort)(from.Y == 0 ? (map.Height - 1) : from.Y - 1);
        var downCoord = (ushort)(from.Y == (map.Height - 1) ? 0 : from.Y + 1);

        switch (direction)
        {
            case Direction.West:
                return new SiteEx(map, leftCoord, from.Y);
            case Direction.East:
                return new SiteEx(map, rightCoord, from.Y);
            case Direction.North:
                return new SiteEx(map, from.X, upCoord);
            case Direction.South:
                return new SiteEx(map, from.X, downCoord);
            case Direction.Still:
            default:
                return from;
        }
    }

    private static Direction GetNearestNonPlayerDirection(Map map, SiteEx activeSite, ushort myID)
    {
        var lastTriedByDirection = new Dictionary<Direction, SiteEx>();
        lastTriedByDirection.Add(Direction.West, activeSite);
        lastTriedByDirection.Add(Direction.East, activeSite);
        lastTriedByDirection.Add(Direction.North, activeSite);
        lastTriedByDirection.Add(Direction.South, activeSite);

        for (int tries = 0; tries < map.Width / 2; tries++)
        {
            foreach (var kvp in lastTriedByDirection.ToArray())
            {
                if (NeighborInDirectionIsNonPlayer(map, kvp.Value, kvp.Key, myID))
                    return kvp.Key;
                else
                    lastTriedByDirection[kvp.Key] = GetNeighbor(map, lastTriedByDirection[kvp.Key], kvp.Key);
            }
        }

        return Direction.Still;
    }

    private static bool NeighborInDirectionIsNonPlayer(Map map, SiteEx site, Direction d, ushort myID)
    {
        var neighborSite = GetNeighbor(map, site, d);
        return neighborSite.Owner != myID;
    }

    private static Direction GetMoveDirection(Map map, SiteEx startSite, SiteEx destinationSite)
    {
        if (destinationSite.X == map.Width - 1 && startSite.X == 0)
            return Direction.West;
        if (destinationSite.X == 0 && startSite.X == map.Width - 1)
            return Direction.East;
        if (destinationSite.X < startSite.X)
            return Direction.West;
        if (destinationSite.X > startSite.X)
            return Direction.East;

        if (destinationSite.Y == map.Height - 1 && startSite.Y == 0)
            return Direction.North;
        if (destinationSite.Y == 0 && startSite.Y == map.Height - 1)
            return Direction.South;
        if (destinationSite.Y < startSite.Y)
            return Direction.North;
        if (destinationSite.Y > startSite.Y)
            return Direction.South;

        throw new Exception("Your destionation is the same as your start site.");
    }

    class SiteEx
    {
        public SiteEx(Map map, ushort x, ushort y)
        {
            Site = map[x, y];
            Location = new Location() { X = x, Y = y };
        }

        public Site Site;
        public Location Location;
        public ushort Owner { get { return Site.Owner; } }
        public ushort Strength { get { return Site.Strength; } }
        public ushort Production { get { return Site.Production; } }
        public ushort X { get { return Location.X; } }
        public ushort Y { get { return Location.Y; } }
    }
}
