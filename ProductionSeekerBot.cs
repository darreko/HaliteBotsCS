using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class ProductionSeekerBot
{
    public const string MyBotName = "ProductionSeekerBot";
    //static ushort[,] _ProductionValues;
    //static string _LogFileName = string.Format("log{0}.txt", DateTime.Now.Ticks);
    private static ushort _MaxProductionValue = 1;
    private static ushort _TopProductionValuesGreaterThan = 1;
    private static ushort _MyID;

    public static void Main(string[] args)
    {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);


        var map = Networking.getInit(out _MyID);
        DarrekLog.LogFileName = String.Format("log{0}.txt", _MyID);
        const ushort MAX_STRENGTH = 255;
        const ushort HALF_STRENGTH = 128;

        /* ------
            Do more prep work, see rules for time limit
        ------ */


        Networking.SendInit(MyBotName); // Acknoweldge the init and begin the game

        var random = new Random();

        DarrekLog.AppendLog("Starting battle!");
        int frameNumber = 0;
        var movesTowardHighProductionByDirection = new Dictionary<Direction, ushort>();
        movesTowardHighProductionByDirection.Add(Direction.North, 0);
        movesTowardHighProductionByDirection.Add(Direction.South, 0);
        movesTowardHighProductionByDirection.Add(Direction.East, 0);
        movesTowardHighProductionByDirection.Add(Direction.West, 0);

        while (true)
        {
            //try
            //{
            Networking.getFrame(ref map); // Update the map to reflect the moves before this turn
            //}
            //catch (Exception)
            //{
            //    return;
            //}

            #region Check Production
            if (frameNumber == 0)
            {
                var sb = new StringBuilder();
                sb.Append("production values:");
                
                //TODO: find nearest high production zone and head there.
                for (ushort y = 0; y < map.Height; y++)
                {
                    sb.Append("\r\n");
                    for (ushort x = 0; x < map.Width; x++)
                    {
                        var prodVal = map[x, y].Production;
                        sb.Append(prodVal + " ");
                        
                        if (_MaxProductionValue < prodVal)
                            _MaxProductionValue = prodVal;
                    }
                }

                DarrekLog.AppendLog(sb.ToString());

                _TopProductionValuesGreaterThan = (ushort)(_MaxProductionValue * .70);

                DarrekLog.AppendLog(string.Format("Max Production Value Found = {0}", _MaxProductionValue));
                DarrekLog.AppendLog(string.Format("Top Production Values >= {0}", _TopProductionValuesGreaterThan));
            }
            #endregion

            frameNumber++;

            DarrekLog.AppendLog(string.Format("Frame {0}", frameNumber));

            movesTowardHighProductionByDirection[Direction.North] = 0;
            movesTowardHighProductionByDirection[Direction.South] = 0;
            movesTowardHighProductionByDirection[Direction.East] = 0;
            movesTowardHighProductionByDirection[Direction.West] = 0;

            var moves = new List<Move>();
            for (ushort x = 0; x < map.Width; x++)
            {
                for (ushort y = 0; y < map.Height; y++)
                {
                    if (map[x, y].Owner == _MyID)
                    {
                        var activeSite = new SiteEx(map, x, y);

                        if (activeSite.Strength < MAX_STRENGTH / 10)
                            continue;

                        var neighbors = new List<SiteEx>()
                        {
                            GetNeighbor(map, activeSite, Direction.West),
                            GetNeighbor(map, activeSite, Direction.East),
                            GetNeighbor(map, activeSite, Direction.North),
                            GetNeighbor(map, activeSite, Direction.South)
                        };

                        var friendlySites = neighbors.Where(c => c.Owner == _MyID);
                        var neutralNeighbors = neighbors.Where(c => c.Owner == 0);
                        var potentialLunchSites =
                            neighbors.Where(c => c.Strength < activeSite.Strength && c.Owner == 0);

                        //1. Try to grow
                        if (potentialLunchSites.Any())
                        {
                            if (neutralNeighbors.Count() >= 3 || potentialLunchSites.Count() > 1)
                            {
                                var directionToMove = GetNearestFreeHighProductionDirection(map, activeSite);
                                movesTowardHighProductionByDirection[directionToMove]++;

                                if (GetNeighbor(map, activeSite, directionToMove).Strength < activeSite.Strength)
                                {
                                    moves.Add(new Move
                                    {
                                        Location = activeSite.Location,
                                        Direction = directionToMove
                                    });
                                }
                            }
                            else
                            {
                                var lunchSite = potentialLunchSites.OrderByDescending(s => s.Production).First();
                                var directionToMove = GetMoveDirection(map, activeSite, lunchSite);

                                moves.Add(new Move
                                {
                                    Location = activeSite.Location,
                                    Direction = directionToMove
                                });

                                continue;
                            }


                        }



                        //2. If all neighbors are friendly, move where most of the blob is moving (toward high production)
                        if (friendlySites.Count() == neighbors.Count && ((activeSite.Strength >= (activeSite.Production * 5)) || activeSite.Strength > HALF_STRENGTH))
                        {
                            ushort distanceFromEdge;
                            var nearestNonPlayerDirection = GetNearestNonPlayerDirection(map, activeSite, _MyID, out distanceFromEdge);

                            if (movesTowardHighProductionByDirection.All(m => m.Value == 0) || distanceFromEdge > 3)
                            {
                                if (nearestNonPlayerDirection != Direction.Still)
                                {
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
                            else
                            {
                                var blobMoveDirection = movesTowardHighProductionByDirection.First(m => m.Value == movesTowardHighProductionByDirection.Max(n => n.Value)).Key;

                                DarrekLog.AppendLog(string.Format("Moving with blob to the {0}", blobMoveDirection));
                                
                                moves.Add(new Move
                                {
                                    Location = activeSite.Location,
                                    Direction = blobMoveDirection
                                });
                            }
                        }
                    }
                }
            }

            Networking.SendMoves(moves); // Send moves

        }

    }

    #region Helper Methods

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

    private static Direction GetNearestFreeHighProductionDirection(Map map, SiteEx activeSite) //TODO: This needs to take into account the allowed directions.
    {
        //DarrekLog.AppendLog(string.Format("GetNearestFreeHighProductionDirection... Active site is [{0},{1}]", activeSite.X, activeSite.Y));

        for (ushort distanceFromOrigin = 1; distanceFromOrigin < map.Width / 2; distanceFromOrigin++)
        {
            //DarrekLog.AppendLog("Distance 1...");
            //make a square around the origin.

            for (var x = distanceFromOrigin * -1; x <= distanceFromOrigin; x++)
            {

                for (var y = distanceFromOrigin * -1; y <= distanceFromOrigin; y++)
                {
                    if ((Math.Abs(y) != distanceFromOrigin) && (Math.Abs(x) != distanceFromOrigin))
                        continue; // don't need to check this space again

                    //DarrekLog.AppendLog("Relative: " + x + ", " + y);


                    var mapX = (ushort)((((int)activeSite.X) + x + map.Width) % map.Width);
                    var mapY = (ushort)((((int)activeSite.Y) + y + map.Height) % map.Height);

                    //DarrekLog.AppendLog(string.Format("Checking map[{0},{1}]...", mapX, mapY));

                    var toCheck = map[mapX, mapY];


                    if (toCheck.Owner != _MyID)// == 0)
                    {
                        //DarrekLog.AppendLog("Found neutral production area!");
                        if (toCheck.Production >= _TopProductionValuesGreaterThan)
                        {

                            DarrekLog.AppendLog(string.Format("Found acceptable neutral production area at [{1},{2}], value = {0}", toCheck.Production, mapX, mapY));
                            if (Math.Abs(x) <= Math.Abs(y))
                            {
                                //DarrekLog.AppendLog("Moving Horizontally towards high production area");
                                if (x < 0)
                                {
                                    DarrekLog.AppendLog("Heading West to HPA");
                                    return Direction.West;
                                }
                                else if (x > 0)
                                {

                                    DarrekLog.AppendLog("Heading East to HPA");
                                    return Direction.East;
                                }
                            }
                            else if (Math.Abs(x) > Math.Abs(y))
                            {
                                //DarrekLog.AppendLog("Moving Vertically towards high production area");
                                if (y < 0)
                                {
                                    DarrekLog.AppendLog("Heading North to HPA");
                                    return Direction.North;
                                }
                                else if (y > 0)
                                {
                                    DarrekLog.AppendLog("Heading South to HPA");
                                    return Direction.South;
                                }
                            }
                            else
                            {
                                DarrekLog.AppendLog("dammit!");
                                return Direction.Still;
                            }
                        }
                    }
                }
            }
        }

        return Direction.Still;
    }

    private static Direction GetNearestNonPlayerDirection(Map map, SiteEx activeSite, ushort myID, out ushort distanceFromEdge)
    {
        var lastTriedByDirection = new Dictionary<Direction, SiteEx>();
        lastTriedByDirection.Add(Direction.West, activeSite);
        lastTriedByDirection.Add(Direction.East, activeSite);
        lastTriedByDirection.Add(Direction.North, activeSite);
        lastTriedByDirection.Add(Direction.South, activeSite);

        for (distanceFromEdge = 0; distanceFromEdge < map.Width / 2; distanceFromEdge++)
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

    #endregion

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

public static class DarrekLog
{
    private static string _LogFileName;
    public static string LogFileName
    {
        get { return _LogFileName; }
        set
        {
            _LogFileName = value;

            if (File.Exists(_LogFileName))
                File.Delete(_LogFileName);

        }
    }
    public static void AppendLog(string line)
    {
        using (var sw = File.AppendText(LogFileName))
        {
            sw.WriteLine(line);
        }
    }
}
