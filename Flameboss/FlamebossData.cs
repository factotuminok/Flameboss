namespace Flameboss;

public static class FlamebossData
{

    static FlamebossData()
    {
        CookStatusList = new List<FlameBossStatus>();
    }

    public static bool Cooking { get; set; }
    public static DateTime LastSuccessfulCheck { get; set; }
    public static DateTime LastPoll { get; set; } 
    public static List<FlameBossStatus> CookStatusList { get; set; }
    public static FlameBossStatus CurrentStatus { get; set; }
    
    public static List<int> GetPitTemperatureList()
    {
        if(FlamebossData.Cooking)
            return FlamebossData.CookStatusList.Select(x => x.Pit).ToList();
        else
        {
            return new List<int>{200, 205, 210, 199, 203};
        }
    }
    
    public static List<int> GetMeatTemperatureList()
    {
        if(FlamebossData.Cooking)
            return FlamebossData.CookStatusList.Select(x => x.Meat1).ToList();
        else
        {
            return new List<int>{145,146,147,148,150};
        }
    }
    
    public static List<int> GetSetTemperatureList()
    {
        if(FlamebossData.Cooking)
            return CookStatusList.Select(x => x.SetTemperature).ToList();
        else
        {
            return new List<int>{250,250,250,250,250};
        }
    }
    
    public static List<int> GetBlowerSpeedList()
    {
        if(Cooking)
            return CookStatusList.Select(x => x.BlowerPercentage).ToList();
        else
        {
            return new List<int>{0,5,10,0,20};
        }
    }

    public static List<DateTime> GetLastUpdateList()
    {
        if(Cooking)
            return CookStatusList.Select(x => x.LastUpdate).ToList();
        else
        {
            return new List<DateTime>{ DateTime.Now.AddSeconds(-25), DateTime.Now.AddSeconds(-20), DateTime.Now.AddSeconds(-15), DateTime.Now.AddSeconds(-10), DateTime.Now.AddSeconds(-5) };
        }
    }

}