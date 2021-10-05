namespace SEScripts {

class Scheduler {
public delegate System SystemMaker(Program program, string name, MyIni ini);

abstract public class System {
    public string Name;
    public string Type;
    public Program program;
    public abstract IEnumerator<double> HandleCommand(ArgParser args);

    public System(Program program, string name, string type) {
        Name=name;
        Type=type;
        this.program=program;
      }

    protected void Log(string message) {
        program.Echo($"{Type}['{Name}']:{message}");
    }

}

public class TimedAction {
    public TimeSpan when;
    public IEnumerator<Double> func;

    public TimedAction next;

    public TimedAction(TimeSpan w, IEnumerator<Double> f) {
        when=w;
        func=f;
        next=null;
    }
}


public TimeSpan runTime;
public IMyTextSurface screen;

public Dictionary<string,System> Systems=new Dictionary<string,System>();

public TimedAction nextAction;

public void QueueAction(TimedAction action) {
    Echo("QueueAction called");
    if (nextAction==null) { nextAction=action; return; }
    if (nextAction.when > action.when) {
        action.next=nextAction;
        nextAction=action;
        return;
    }
    TimedAction after=nextAction;
    while (after.next!=null && (after.next.when < action.when)) {
        after=after.next;
    }
    action.next=after.next;
    after.next=action;

}

public class ArgParser {
    public string function;
    public string system;
    int nextIndex;

    string[] args;

    public string error;
    string baseError;

    public void Prep(string[] args) {
        this.args=args;
        system=args[0];
        function=args.Length>1?args[1]:"";
        nextIndex=2;
        error="";
        baseError=$"{system}::{function}: ";
    }
    public void Expect(int n) {
        if(n!=args.Length-2) {
            error=$"Expected {n} args, got {args.Length-2}!";
        }
    }

    //$"{Type}['{Name}']:{message}");
    public string NextString() {
        if(error!="" || nextIndex==args.Length) {
            error=baseError+"not enough arguments";
            return "";
        } else {
            return args[nextIndex++];
        }
    }
    public double NextDouble() {
        double dbl=0;
        if(error!="") {}
        else if(nextIndex==args.Length) {
            error=baseError+"not enough arguments";
        }
        else if (!Double.TryParse(args[nextIndex++],out dbl)) {
            error=baseError+$"arg #{nextIndex-1}: expected double, got '{args[nextIndex]}'!";
        }
        return dbl;
    }

}

ArgParser parser;

public Program() {
    runTime=Runtime.TimeSinceLastRun;
    screen = Me.GetSurface(1);
    screen.ContentType=ContentType.TEXT_AND_IMAGE;
    screen.FontSize=3.0F;
    parser=new ArgParser();

    var ini=new MyIni();
    MyIniParseResult iniResult;
    if (!ini.TryParse(Me.CustomData,out iniResult)) {
        Echo($"Ini error: {iniResult}");
    } else {
        var systemNames=new List<string>();
        ini.GetSections(systemNames);
        foreach (var sysName in systemNames) {
            var type=ini.Get(sysName,"type");
            SystemMaker maker;
            if (type.IsEmpty || !SystemTypes.TryGetValue(type.ToString(),out maker)) {
                Echo($"Ini error: missing or invalid type in system {sysName}!");
                continue;
            }
            Systems.Add(sysName,maker(this,sysName,ini));
        }
    }

    Runtime.UpdateFrequency=UpdateFrequency.None;
}

public void Save() {
}

public void RunAction(TimedAction action) {
    action.func.MoveNext();

    if(action.func.Current!=0.0) {
        //if it's not done, queue it up to continue
        action.when+=TimeSpan.FromSeconds(action.func.Current/60);
        QueueAction(action);
    }
}

public void Main(string argument, UpdateType updateSource) {
    runTime += Runtime.TimeSinceLastRun;
    screen.WriteText($"{runTime}\n",false);

    if((updateSource & (UpdateType.Trigger | UpdateType.Terminal))!=0) {
        string[] args=argument.Split(',');
        for (var i=0;i<args.Length;i++) {
            args[i]=args[i].Trim();
        }

        parser.Prep(args);
        //commands begin with the name of the system being commanded.
        System cmdTarget;

        if(Systems.TryGetValue(parser.system,out cmdTarget)) {
            var handler=cmdTarget.HandleCommand(parser);
            if(handler!=null) {
                TimedAction newAction=new TimedAction(runTime,handler);
                RunAction(newAction);
            }
        } else {
            //core system commands
            switch(args[0]) {
                case "types": {
                    string str="";
                    foreach(string key in SystemTypes.Keys) {
                        str+=key+" ";
                    }
                    Echo(str);
                    break;
                }
                case "systems": {
                    string str="";
                    foreach(var system in Systems.Values) {
                        str+=$"{system.Name}({system.Type})\n";
                    }   
                    Echo(str);
                    break;
                }
            }
        }
    } else {
        while(nextAction!=null && nextAction.when <= runTime) {
            //pop next action
            var action=nextAction;
            nextAction=action.next;
            //do the thing
            RunAction(action);
            //TODO: monitor spent cycles, end early and let things
            //run late if necessary? Feels unlikely to come up
            //any time soon, but I may do more, and more expensive,
            //systems later...
        }
    }

    if (nextAction==null) {
        Echo("No actions remaining!");
        Runtime.UpdateFrequency=UpdateFrequency.None;
    } else {
        if((nextAction.when-runTime).TotalSeconds*60 < 90) {
            Runtime.UpdateFrequency=UpdateFrequency.Update10;
        } else {
            Runtime.UpdateFrequency=UpdateFrequency.Update100;
        }
    }
}
}
}