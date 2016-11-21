using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class SnakeBot
{
    public const string MyBotName = "Snake Bot";
    //static ushort[,] _ProductionValues;
    //static string _LogFileName = string.Format("log{0}.txt", DateTime.Now.Ticks);
    private static ushort _MaxProductionValue = 1;
    private static ushort _TopProductionValuesGreaterThan = 1;
    private static ushort _MyID;
    private const ushort MAX_SNAKE_LENGTH = 4;
    private static ushort _MaxScanDistance = 7;
    const ushort MAX_STRENGTH = 255;
    const ushort HALF_STRENGTH = 128;

    public static void Main(string[] args)
    {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);

        try
        {
            var map = Networking.getInit(out _MyID);
            DarreksLog.LogFileName = String.Format("log{0}.txt", _MyID);
            _MaxScanDistance = (ushort)(map.Width / 4);





            #region Check Production before game start

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

            //DarreksLog.AppendLog(sb.ToString());

            _TopProductionValuesGreaterThan = (ushort)(_MaxProductionValue * .75);

            DarreksLog.AppendLog(string.Format("Max Production Value Found = {0}", _MaxProductionValue));
            DarreksLog.AppendLog(string.Format("Top Production Values >= {0}", _TopProductionValuesGreaterThan));

            #endregion










            LinkedList<Location> currentSnakePath = null;
            bool currentSnakePathIsCompleted = true;

            /* ------
                Do more prep work, see rules for time limit
            ------ */

            sb.Clear();
            sb.Append("Value to Me:");
            var valueMap = new short[map.Width, map.Height];
            for (ushort y = 0; y < map.Height; y++)
            {
                sb.Append("\r\n");
                for (ushort x = 0; x < map.Width; x++)
                {
                    var activeSite = map[x, y];
                    valueMap[x, y] = GetValueOfSite(activeSite);
                    sb.Append(valueMap[x, y] + " ");
                }
            }
            //DarreksLog.AppendLog(sb.ToString());

            var gaussianValueMap = GetGaussianValueMap(valueMap, map.Width, map.Height);

            sb.Clear();
            sb.Append("Gaussian Values:");
            for (ushort y = 0; y < map.Height; y++)
            {
                sb.Append("\r\n");
                for (ushort x = 0; x < map.Width; x++)
                {
                    sb.Append(gaussianValueMap[x, y] + " ");
                }
            }

            DarreksLog.AppendLog(sb.ToString());
            
            Networking.SendInit(MyBotName); // Acknoweldge the init and begin the game

            var random = new Random();

            DarreksLog.AppendLog("Starting battle!");
            int frameNumber = 0;
            Location startLocation = new Location(0, 0);

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
                DarreksLog.AppendLog(string.Format("Frame {0}", frameNumber));

                var moves = new HashSet<Move>();

                
                for (ushort x = 0; x < map.Width; x++)
                {
                    for (ushort y = 0; y < map.Height; y++)
                    {
                        if (map[x, y].Owner == _MyID)
                        {
                            //DarreksLog.AppendLog("GetProductionSeekerBotMove...");
                            var move = GetProductionSeekerBotMove(map, x, y);
                            if (move.HasValue)
                            {
                                //DarreksLog.AppendLog(String.Format("Added Move {0},{1}->{2}...", move.Value.Location.X, move.Value.Location.Y, move.Value.Direction));
                                moves.Add(move.Value);
                            }

                            if (frameNumber == 1)
                            {
                               startLocation = new Location(x, y);
                            }
                        }
                    }
                }



                #region Pick new Snake Destination

                if (currentSnakePathIsCompleted && frameNumber < 10)
                {
                    //we have a notion of the "head" of the line (linkedlist), and also which locations are feeding into it. These locations should all just send their resources through towards the head.
                    //TODO: when the snake moves, the new location becomes the head, and drop the tail if longer than the limit

                    //TODO: as the size grows, multiple lines could exist to search out the best production zones.


                    currentSnakePathIsCompleted = false;

                    DarreksLog.AppendLog("Setting Snake Destination...");

                    var snakeStartLocation = currentSnakePath == null
                        ? startLocation
                        : currentSnakePath.Last.Value;

                    DarreksLog.AppendLog(string.Format("Starting at {0},{1}", snakeStartLocation.X, snakeStartLocation.Y));
                    var snakeDestination = GetNearestFreeHighProductionDirection(map, gaussianValueMap, snakeStartLocation);
                    DarreksLog.AppendLog(string.Format("Snake Destination Set to {0},{1}. Production there is {2}", snakeDestination.X, snakeDestination.Y, map[snakeDestination].Production));

                    DarreksLog.AppendLog("Getting Snake's Best Path To Destination...");

                    var newPathBits = GetBestPathToLocation(map, gaussianValueMap, snakeStartLocation, snakeDestination);
                    if (currentSnakePath == null)
                    {
                        currentSnakePath = newPathBits;
                    }
                    else
                    {
                        foreach (var newPathBit in newPathBits)
                        {
                            if (!currentSnakePath.Last.Value.Equals(newPathBit))
                                currentSnakePath.AddLast(newPathBit);
                        }


                        //Remove duplicates in the path
                        var locationsInSnake = new HashSet<Location>();
                        var looker = currentSnakePath.Last;

                        do
                        {
                            DarreksLog.AppendLog("Checking for looping in the snake.");

                            if (locationsInSnake.Contains(looker.Value))
                            {
                                DarreksLog.AppendLog(string.Format("Found a loop on {0},{1}.", looker.Value.X, looker.Value.Y));
                                do
                                {
                                    currentSnakePath.RemoveFirst();
                                } while (!currentSnakePath.First.Value.Equals(looker.Value));
                                currentSnakePath.RemoveFirst();
                                if (currentSnakePath.Count <= 1)
                                    currentSnakePathIsCompleted = true;
                            }
                            else
                            {
                                locationsInSnake.Add(looker.Value);
                            }

                            looker = looker.Previous;
                        } while (looker != null);


                        DarreksLog.AppendLog("Path To Destination:");

                        foreach (var node in currentSnakePath)
                            DarreksLog.AppendLog(string.Format("{0},{1}", node.X, node.Y));
                    }
                    
                    DarreksLog.AppendLog(string.Format("The New Snake Path is {0} feet long!", currentSnakePath.Count));
                }

                #endregion

                #region Snake Movement

                currentSnakePathIsCompleted = MakeSnakeMovements(currentSnakePath, map, currentSnakePathIsCompleted, moves);

                #endregion Snake Movement

                Networking.SendMoves(moves); // Send moves

            }
        }
        catch (Exception ex)
        {
            DarreksLog.AppendLog("Exception Happened: " + ex.Message + "\r\nat\r\n" + ex.StackTrace);
        }
    }

    public static short[,] GetGaussianValueMap(short[,] valueMap, ushort width, ushort height)
    {
        var gaussianValueMap = new short[width, height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {

                var westValue = valueMap[(x - 1 + width)%width, y];
                var eastValue = valueMap[(x + 1) % width, y];
                var northValue = valueMap[x, (y - 1 + height) % height];
                var southValue = valueMap[x, (y + 1) % height];
                var value = valueMap[x, y];

                int gaussianValue = value + westValue + eastValue + northValue + southValue;
                if (gaussianValue < short.MinValue)
                    gaussianValue = short.MinValue;
                if (gaussianValue > short.MaxValue)
                    gaussianValue = short.MaxValue;

                gaussianValueMap[x, y] = (short)gaussianValue;
            }
        }

        return gaussianValueMap;
    }

    private static bool MakeSnakeMovements(LinkedList<Location> currentSnakePath, Map map, bool currentSnakePathIsCompleted, HashSet<Move> moves)
    {
        var numberOfFeetToTrimSnake = 0;

        if (currentSnakePath == null)
        {
            DarreksLog.AppendLog("SnakePath cannot be null.");
        }

        DarreksLog.AppendLog("Performing Snake Movement...");

        var positionOnPath = currentSnakePath.Last;
        if (map[positionOnPath.Value].Owner == _MyID)
        {
            DarreksLog.AppendLog("It appears we have navigated to the destination! HUZZAH!");
            currentSnakePathIsCompleted = true;
        }
        else
        {
            var numSnakePieces = 0;

            while (positionOnPath != null)
            {
                var activeSite = map[positionOnPath.Value];
                //if a site on the path is mine, move it along the path if strong enough.
                if (activeSite.Owner == _MyID)
                {
                    var thisGuysDestination = positionOnPath.Next;

                    if ((thisGuysDestination == null) || (activeSite.Strength == 0))
                    {
                        //Don't move a snake that doesn't have any strength.
                    }
                    else
                    {
                        numSnakePieces++;

                        if (numSnakePieces > MAX_SNAKE_LENGTH)
                            numberOfFeetToTrimSnake++;

                        var dir = GetMoveDirection(map, positionOnPath.Value, thisGuysDestination.Value);
                        var destinationSite = GetNeighbor(map, positionOnPath.Value, dir);

                        if ((destinationSite.Owner != _MyID && destinationSite.Strength > activeSite.Strength) ||
                            (activeSite.Strength < activeSite.Production*3))
                        {
                            DarreksLog.AppendLog(string.Format("Staying Here at {0},{1}.", positionOnPath.Value.X,
                                positionOnPath.Value.Y));

                            //Don't move if this will kill the site. 
                            //Also don't move if we are super small.
                            //A still move to override other movements if they exist.
                            moves.Add(new Move
                            {
                                Location = positionOnPath.Value,
                                Direction = Direction.Still
                            });
                        }
                        else
                        {
                            DarreksLog.AppendLog(string.Format("Moving {2} from {0},{1}.", positionOnPath.Value.X,
                                positionOnPath.Value.Y, dir));
                            moves.Add(new Move
                            {
                                Location = positionOnPath.Value,
                                Direction = dir
                            });
                        }
                    }
                }

                positionOnPath = positionOnPath.Previous;
            }

            DarreksLog.AppendLog(string.Format("The Snake is {0} feet long!", numSnakePieces));
        }

        if (numberOfFeetToTrimSnake > 0)
        {
            DarreksLog.AppendLog(string.Format("The Snake is too long. Trimming by {0} Feet", numberOfFeetToTrimSnake));
            for (var i = 0; i < numberOfFeetToTrimSnake; i++)
                currentSnakePath.RemoveFirst();
        }

        DarreksLog.AppendLog("Done Performing Snake Movement.");
        return currentSnakePathIsCompleted;
    }

    private static Move? GetProductionSeekerBotMove(Map map, ushort x, ushort y)
    {
        //Borrows the code from ProductionSeekerBot2

        var activeSite = new SiteEx(map, x, y);

        if ((activeSite.Strength < activeSite.Production * 3) && (activeSite.Strength < MAX_STRENGTH / 12))
            return null;

        var neighbors = new List<SiteEx>()
                        {
                            GetNeighbor(map, activeSite.Location, Direction.West),
                            GetNeighbor(map, activeSite.Location, Direction.East),
                            GetNeighbor(map, activeSite.Location, Direction.North),
                            GetNeighbor(map, activeSite.Location, Direction.South)
                        };

        var friendlySites = neighbors.Where(c => c.Owner == _MyID);
        var friendlySitesWorthVisiting =
            friendlySites.Where(
                f =>
                    f.Strength > 0 && f.Strength + activeSite.Strength < MAX_STRENGTH &&
                    f.Production > (_MaxProductionValue / 4));
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
                var directionToMove = GetMoveDirection(map, activeSite.Location, destination.Location);
                DarreksLog.AppendLog("Combine Forces!");

                return new Move
                {
                    Location = activeSite.Location,
                    Direction = directionToMove
                };
            }
        }

        //1. Try to grow
        if (weakNeighborSites.Any())
        {
            SiteEx lunchSiteToAttack = null;

            //Move Between enemies if possible for maximum damage!
            foreach (var pls in weakNeighborSites)
            {
                var nW = GetNeighbor(map, pls.Location, Direction.West);
                var nE = GetNeighbor(map, pls.Location, Direction.East);
                var nS = GetNeighbor(map, pls.Location, Direction.South);
                var nN = GetNeighbor(map, pls.Location, Direction.North);

                var enemyNeighborsCount = ((nW.Owner != _MyID && nW.Owner != 0) ? 1 : 0) +
                                     ((nE.Owner != _MyID && nE.Owner != 0) ? 1 : 0) +
                                     ((nS.Owner != _MyID && nS.Owner != 0) ? 1 : 0) +
                                     ((nN.Owner != _MyID && nN.Owner != 0) ? 1 : 0);

                if (enemyNeighborsCount > 1)
                    lunchSiteToAttack = pls;
            }

            if (lunchSiteToAttack == null)
                lunchSiteToAttack = weakNeighborSites.OrderByDescending(s => s.Production - s.Strength / 10).First();

            var directionToMove = GetMoveDirection(map, activeSite.Location, lunchSiteToAttack.Location);

            if (lunchSiteToAttack.Strength < activeSite.Strength)
            {
                return new Move
                {
                    Location = activeSite.Location,
                    Direction = directionToMove
                };
            }
        }


        //2. If all neighbors are friendly, move towards nearest edge.
        else if (activeSite.Strength > 0 && friendlySites.Count() == neighbors.Count && ((activeSite.Strength >= (activeSite.Production * 3)) || activeSite.Strength > HALF_STRENGTH / 2))
        {
            ushort distanceFromEdge;
            var nearestNonPlayerDirection = GetNearestNonPlayerDirection(map, activeSite, _MyID, out distanceFromEdge);
            if (nearestNonPlayerDirection != Direction.Still)
            {
                return new Move
                {
                    Location = activeSite.Location,
                    Direction = nearestNonPlayerDirection
                };
            }
            else //All orthagonal directions are occupied.
            {
                return new Move
                {
                    Location = activeSite.Location,
                    Direction = x % 2 == 1 ? Direction.North : Direction.South
                };
                //TODO: could have logic here to find enemies or something, or go diagonally.
            }
        }

        return null;
    }

    #region Helper Methods

    static SiteEx GetNeighbor(Map map, Location from, Direction direction)
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
                return new SiteEx(map, from.X, from.Y);
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
                    lastTriedByDirection[kvp.Key] = GetNeighbor(map, lastTriedByDirection[kvp.Key].Location, kvp.Key);
            }
        }

        return Direction.Still;
    }

    public static LinkedList<Location> GetBestPathToLocation(Map map, short[,] gaussianValueMap, Location startLocation, Location destinationLocation)
    {
        //var path = Pathing.FindQuickestPath(gaussianValueMap, new Tuple<int, int>(startLocation.X, startLocation.Y),
        //    new Tuple<int, int>(destinationLocation.X, destinationLocation.Y));




        //could use solely the sites in a box surrounding the start and destination, then grabbing the smallest 



        //TODO: find cheapest route.
        DarreksLog.AppendLog(string.Format("Getting Best Path From {0},{1} to {2},{3}...", startLocation.X, startLocation.Y, destinationLocation.X, destinationLocation.Y));

        var path = new LinkedList<Location>();
        path.AddFirst(startLocation);

        var positionOnPath = startLocation;

        while (!positionOnPath.Equals(destinationLocation))
        {
            var xDistance = positionOnPath.X - destinationLocation.X;
            var yDistance = positionOnPath.Y - destinationLocation.Y;
            var crossesMapEdgeX = false;
            var crossesMapEdgeY = false;

            if (Math.Abs(xDistance) > map.Width/2)
                crossesMapEdgeX = true;
            if (Math.Abs(yDistance) > map.Height/2)
                crossesMapEdgeY = true;

            DarreksLog.AppendLog(string.Format("From {0},{1} to {2},{3}...", positionOnPath.X, positionOnPath.Y, destinationLocation.X, destinationLocation.Y));
            DarreksLog.AppendLog(string.Format("xDistance = {0}, yDistance = {1}, crossesXEdge = {2}, crossesYEdge = {3}", xDistance, yDistance, crossesMapEdgeX, crossesMapEdgeY));


            Location? potentialPositionMovingHorizontally = null;
            Location? potentialPositionMovingVertically = null;

            if (xDistance != 0)
            {
                bool moveWest = false;

                if (positionOnPath.X > destinationLocation.X)
                {
                    DarreksLog.AppendLog("The destination is to the west.");
                    moveWest = !crossesMapEdgeX;
                }
                else
                {

                    DarreksLog.AppendLog("The destination is to the east.");
                    moveWest = crossesMapEdgeX;
                }

                DarreksLog.AppendLog(string.Format("I am {0}moving to the west", moveWest ? "" : "not "));

                if (!moveWest)
                    potentialPositionMovingHorizontally = new Location((ushort)((positionOnPath.X + 1 + map.Width) % map.Width), positionOnPath.Y);
                else
                    potentialPositionMovingHorizontally = new Location((ushort)((positionOnPath.X - 1 + map.Width) % map.Width), positionOnPath.Y);
            }
            if (yDistance != 0)
            {
                bool moveNorth = false;

                if (positionOnPath.Y > destinationLocation.Y)
                {
                    DarreksLog.AppendLog("The destination is to the north.");
                    moveNorth = !crossesMapEdgeY;
                }
                else
                {

                    DarreksLog.AppendLog("The destination is to the south.");
                    moveNorth = crossesMapEdgeY;
                }
                
                DarreksLog.AppendLog(string.Format("I am {0}moving to the north", moveNorth ? "" : "not "));

                if (!moveNorth)
                    potentialPositionMovingVertically = new Location(positionOnPath.X, (ushort)((positionOnPath.Y + 1 + map.Height) % map.Height));
                else
                    potentialPositionMovingVertically = new Location(positionOnPath.X, (ushort)((positionOnPath.Y - 1 + map.Height) % map.Height));
            }

            if (potentialPositionMovingHorizontally.HasValue && potentialPositionMovingVertically.HasValue)
            {
                //Make a decision here to go vertically or horizontally first.
                var verticalMoveValue =
                    gaussianValueMap[potentialPositionMovingVertically.Value.X, potentialPositionMovingVertically.Value.Y];
                var horizontalMoveValue =
                    gaussianValueMap[potentialPositionMovingHorizontally.Value.X, potentialPositionMovingHorizontally.Value.Y];

                if (verticalMoveValue > horizontalMoveValue)
                    path.AddAfter(path.Last, potentialPositionMovingVertically.Value);
                else
                    path.AddAfter(path.Last, potentialPositionMovingHorizontally.Value);
            }
            else if (potentialPositionMovingVertically.HasValue)
            {
                path.AddAfter(path.Last, potentialPositionMovingVertically.Value);
            }
            else if (potentialPositionMovingHorizontally.HasValue)
            {
                path.AddAfter(path.Last, potentialPositionMovingHorizontally.Value);
            }
            
            positionOnPath = path.Last.Value;
            DarreksLog.AppendLog(string.Format("Added {0},{1} to Path.", positionOnPath.X, positionOnPath.Y));
        }
        return path;
    }
    
    private static Location GetNearestFreeHighProductionDirection(Map map, short[,] gaussianValueMap, Location startLocation) //TODO: This needs to take into account the allowed directions.
    {
        //I think it should value the production as Max production value - distance * 2.... 
        //A production value of 10 that is 5 spaces away would be valued at 10 - (5*2) = 0
        //A production value of 10 that is 4 spaces away would be valued at 10 - (4*2) = 2
        //A production value of 7 that is 4 spaces away would be 7-(4*2) = -1
        //A production value of 7 that is 3 spaces away would be 7- (3*2) = 1

        Location? bestDestination = null;
        var valueAtBestDestination = short.MinValue;


        DarreksLog.AppendLog(string.Format("GetNearestFreeHighProductionDirection... Active site is [{0},{1}]", startLocation.X, startLocation.Y));

        for (var x = _MaxScanDistance * -1; x <= _MaxScanDistance; x++)
        {
            for (var y = _MaxScanDistance * -1; y <= _MaxScanDistance; y++)
            {
                //DarreksLog.AppendLog("Checking Space Relative By: " + x + ", " + y);

                var mapX = (ushort)((((int)startLocation.X) + x + map.Width) % map.Width);
                var mapY = (ushort)((((int)startLocation.Y) + y + map.Height) % map.Height);

                //DarreksLog.AppendLog(string.Format("Checking map[{0},{1}]...", mapX, mapY));

                var toCheck = map[mapX, mapY];


                if (toCheck.Owner != _MyID)// == 0)
                {
                    //TODO: return this to normal... just for testing I am making this find the closest production zone to me with the max production.

                    short valueToMe;
                    var temp = (gaussianValueMap[mapX, mapY] - (35 * ((Math.Abs(x) + Math.Abs(y))))); //No matter how awesome a location is, if it's too far away, you'll never get there.
                    if (temp < short.MinValue)
                        valueToMe = short.MinValue;
                    else if (temp > short.MaxValue)
                        valueToMe = short.MaxValue;
                    else
                        valueToMe = (short)temp; //(short)(toCheck.Production - ((Math.Abs(x) + Math.Abs(y)) * 0.6));

                    //DarreksLog.AppendLog(string.Format("This space has production {0}, and is valued to me at {1}", toCheck.Production, valueToMe));

                    if (valueToMe > valueAtBestDestination)
                    {
                        DarreksLog.AppendLog(string.Format("This is the best site I've checked yet: {0},{1}. It has a gaussian value of {2} and a value to me of {3}!", mapX, mapY, gaussianValueMap[mapX, mapY], valueToMe));

                        bestDestination = new Location() { X = mapX, Y = mapY };
                        valueAtBestDestination = valueToMe;
                    }
                }
            }
        }


        if (!bestDestination.HasValue)
        {
            throw new Exception("How is there not a best destination?");
        }

        return bestDestination.Value;
    }

    //This is used to determine how worth it a given site is for taking
    public static short GetValueOfSite(Site site)
    {
        if (site.Production == 0)
            return (short)(short.MinValue + (MAX_STRENGTH - site.Strength));

        var initialValue = site.Production * 100;

        var timeToRegainStrength = site.Strength/site.Production;
        var siteValue = initialValue - timeToRegainStrength;
        return (short)siteValue;
    }

    private static bool NeighborInDirectionIsNonPlayer(Map map, SiteEx site, Direction d, ushort myID)
    {
        var neighborSite = GetNeighbor(map, site.Location, d);
        return neighborSite.Owner != myID;
    }

    private static Direction GetMoveDirection(Map map, Location startLocation, Location destinationLocation)
    {
        if (destinationLocation.X == map.Width - 1 && startLocation.X == 0)
            return Direction.West;
        if (destinationLocation.X == 0 && startLocation.X == map.Width - 1)
            return Direction.East;
        if (destinationLocation.X < startLocation.X)
            return Direction.West;
        if (destinationLocation.X > startLocation.X)
            return Direction.East;

        if (destinationLocation.Y == map.Height - 1 && startLocation.Y == 0)
            return Direction.North;
        if (destinationLocation.Y == 0 && startLocation.Y == map.Height - 1)
            return Direction.South;
        if (destinationLocation.Y < startLocation.Y)
            return Direction.North;
        if (destinationLocation.Y > startLocation.Y)
            return Direction.South;

        throw new Exception("Your destination is the same as your start site.");
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

public static class DarreksLog
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
        //using (var sw = File.AppendText(LogFileName))
        //{
        //    sw.WriteLine(line);
        //}
    }
}
