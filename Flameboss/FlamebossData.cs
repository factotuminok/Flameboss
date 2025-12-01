namespace Flameboss;

public static class FlamebossData
{

    static FlamebossData()
    {
        CurrentStatus = new FlameBossStatus()
        {
            BlowerPercentage = 0,
            Pit = 0,
            Meat1 = 0,
            SetTemperature = 0,
            LastUpdate = DateTime.Now
        };
        CookStatusList = new List<FlameBossStatus>();
    }

    public static bool Cooking { get; set; }
    public static DateTime LastSuccessfulCheck { get; set; }
    public static DateTime LastPoll { get; set; } 
    public static List<FlameBossStatus> CookStatusList { get; set; }
    public static FlameBossStatus CurrentStatus { get; set; }
    
    public static List<int> GetPitTemperatureList()
    {
        return FlamebossData.CookStatusList.Select(x => x.Pit).ToList();
    }
    
    public static List<int> GetMeatTemperatureList()
    {
        return FlamebossData.CookStatusList.Select(x => x.Meat1).ToList();
    }
    
    public static List<int> GetSetTemperatureList()
    {
        return CookStatusList.Select(x => x.SetTemperature).ToList();
    }
    
    public static List<int> GetBlowerSpeedList()
    {
        return CookStatusList.Select(x => x.BlowerPercentage).ToList();
        
    }

    public static List<DateTime> GetLastUpdateList()
    {
        return CookStatusList.Select(x => x.LastUpdate).ToList();
    }

}