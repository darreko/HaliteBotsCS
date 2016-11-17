using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class ProductionSeekerBot2
{
    public const string MyBotName = "ProductionSeekerBot2";
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

            var moves = new List<Move>();
            for (ushort x = 0; x < map.Width; x++)
            {
                for (ushort y = 0; y < map.Height; y++)
                {
                    if (map[x, y].Owner == _MyID)
                    {
                        var activeSite = new SiteEx(map, x, y);

                        if ((activeSite.Strength < activeSite.Production * 3) && (activeSite.Strength < MAX_STRENGTH / 12))
                            continue;

                        var neighbors = new List<SiteEx>()
                        {
                            GetNeighbor(map, activeSite, Direction.West),
                            GetNeighbor(map, activeSite, Direction.East),
                            GetNeighbor(map, activeSite, Direction.North),
                            GetNeighbor(map, activeSite, Direction.South)
                        };

                        var friendlySites = neighbors.Where(c => c.Owner == _MyID);
                        var friendlySitesWorthVisiting =
                            friendlySites.Where(
                                f =>
                                    f.Strength > 0 && f.Strength + activeSite.Strength < MAX_STRENGTH &&
                                    f.Production > (_MaxProductionValue/4)); 
                        var neutralNeighbors = neighbors.Where(c => c.Owner == 0);
                        var weakNeighborSites = neighbors.Where(c => (activeSite.Strength == MAX_STRENGTH || c.Production > 0) && c.Strength < activeSite.Strength && c.Owner == 0);

                        bool shouldCombine = false;

                        if (!weakNeighborSites.Any())
                        {
                            if (activeSite.X + activeSite.Y % 2 == 1) //Only combine from every other square to prevent dancing.
                                shouldCombine = true;
                        }

                        //Combine forces if we are a thin line.
                        if (shouldCombine && friendlySitesWorthVisiting.Any())
                        {
                            var destination = friendlySitesWorthVisiting.FirstOrDefault(f => f.Production > activeSite.Production);
                            if (destination != null)
                            {
                                var directionToMove = GetMoveDirection(map, activeSite, destination);
                                DarrekLog.AppendLog("Combine Forces!");

                                moves.Add(new Move
                                {
                                    Location = activeSite.Location,
                                    Direction = directionToMove
                                });
                                continue;
                            }
                        }

                        //1. Try to grow
                        if (weakNeighborSites.Any())
                        {
                            SiteEx lunchSiteToAttack = null;

                            //Move Between enemies if possible for maximum damage!
                            foreach (var pls in weakNeighborSites)
                            {
                                var nW = GetNeighbor(map, pls, Direction.West);
                                var nE = GetNeighbor(map, pls, Direction.East);
                                var nS = GetNeighbor(map, pls, Direction.South);
                                var nN = GetNeighbor(map, pls, Direction.North);

                                var enemyNeighborsCount = ((nW.Owner != _MyID && nW.Owner != 0) ? 1 : 0) +
                                                     ((nE.Owner != _MyID && nE.Owner != 0) ? 1 : 0) +
                                                     ((nS.Owner != _MyID && nS.Owner != 0) ? 1 : 0) +
                                                     ((nN.Owner != _MyID && nN.Owner != 0) ? 1 : 0);

                                if (enemyNeighborsCount > 1)
                                    lunchSiteToAttack = pls;
                            }

                            if (lunchSiteToAttack == null)
                                lunchSiteToAttack = weakNeighborSites.OrderByDescending(s => s.Production - s.Strength/10).First();

                            var directionToMove = GetMoveDirection(map, activeSite, lunchSiteToAttack);

                            if (lunchSiteToAttack.Strength < activeSite.Strength)
                            {
                                moves.Add(new Move
                                {
                                    Location = activeSite.Location,
                                    Direction = directionToMove
                                });
                                continue;
                            }
                        }
                        

                        //2. If all neighbors are friendly, move towards nearest edge.
                        else if (activeSite.Strength > 0 && friendlySites.Count() == neighbors.Count && ((activeSite.Strength >= (activeSite.Production * 3)) || activeSite.Strength > HALF_STRENGTH / 2))
                        {
                            ushort distanceFromEdge;
                            var nearestNonPlayerDirection = GetNearestNonPlayerDirection(map, activeSite, _MyID, out distanceFromEdge);
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
                                moves.Add(new Move
                                {
                                    Location = activeSite.Location,
                                    Direction = x % 2 == 1 ? Direction.North : Direction.South
                                });
                                //TODO: could have logic here to find enemies or something, or go diagonally.
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
