C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\csc.exe /t:library /out:HaliteHelper.dll HaliteHelper.cs
C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\csc.exe /reference:HaliteHelper.dll -out:SnakeBot.exe SnakeBot.cs
halite -d "30 30" "ProductionSeekerBot2.exe" "SnakeBot.exe" 
