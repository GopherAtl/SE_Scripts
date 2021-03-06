public delegate System MakeSystem(Program program, string[] args);
abstract public class System {
    public string Name;
    public string Type;
    public Program program;
    public abstract string StorageStr();
    public abstract IEnumerator<double> HandleCommand(string[] args);
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
public void LoadSystem(string storageStr) {
    string[]args = storageStr.Split();
    MakeSystem maker;
    SystemTypes.TryGetValue(args[0], out maker);
    if(maker==null) {
        Echo($"Tried to load unknown system type \"{args[0]}\"");
        return;
    }
    System newSys=maker(this, args);
    Systems.Add(newSys.Name,newSys);
}
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

public Program() {
    runTime=Runtime.TimeSinceLastRun;
    screen = Me.GetSurface(1);
    screen.ContentType=ContentType.TEXT_AND_IMAGE;
    screen.FontSize=3.0F;
    string[] systemDefs=Storage.Split('\n');
    switch (systemDefs[0]) {
        case "StorageV1":
            for (var i=1;i<systemDefs.Length;i++) {
                var str=systemDefs[i];
                if(str=="") continue;
                Echo($"def: {str}");
                string[] args=str.Split(',');
                MakeSystem maker;
                System chode;
                if (!SystemTypes.TryGetValue(args[0],out maker)) {
                    Echo($"Initialization error: unknown system type '{args[0]}'");
                } else if(Systems.TryGetValue(args[1], out chode)) {
                    Echo($"Initialization error: Already have a system named '{args[1]}'");
                } else {
                    Systems.Add(args[1],maker(this,args));
                }
            }
            break;
        default:
            foreach (string str in systemDefs) {
                if(str=="") continue;
                string[] args=str.Split(' ');
                Echo($"def: {str}");
                MakeSystem maker;
                System chode;
                if (!SystemTypes.TryGetValue(args[0],out maker)) {
                    Echo($"Initialization error: unknown system type '{args[0]}'");
                } else if(Systems.TryGetValue(args[1], out chode)) {
                    Echo($"Initialization error: Already have a system named '{args[1]}'");
                } else {
                    Systems.Add(args[1],maker(this,args));
                }
            }
            break;
    }
    Runtime.UpdateFrequency=UpdateFrequency.None;
}

public void Save() {
    Echo("Save called");
    string[] storageStrs=new string[Systems.Count()+1];
    storageStrs[0]="StorageV1";
    int i=0;
    foreach (System sys in Systems.Values) {
        storageStrs[i+1]=sys.StorageStr();
        Echo(storageStrs[i+1]);
        i++;
    }
    Storage=String.Join("\n",storageStrs);
}
public void RunAction(TimedAction action) {
    action.func.MoveNext();
    if(action.func.Current!=0.0) {
        action.when+=TimeSpan.FromSeconds(action.func.Current/60);
        QueueAction(action);
    }
}
public void Main(string argument, UpdateType updateSource) {
    runTime += Runtime.TimeSinceLastRun;
    screen.WriteText($"{runTime}\n",false);
    if(updateSource==UpdateType.Trigger || updateSource==UpdateType.Terminal) {
        string[] args=argument.Split(',');
        for (var i=0;i<args.Length;i++) {
            args[i]=args[i].Trim();
        }
        System cmdTarget;
        if(Systems.TryGetValue(args[0],out cmdTarget)) {
            var handler=cmdTarget.HandleCommand(args);
            if(handler!=null) {
                TimedAction newAction=new TimedAction(runTime,handler);
                RunAction(newAction);
            }
        } else {
            switch(args[0]) {
                case "save":
                Save();
                break;
            case "load":
                Echo(Storage);
                break;
            case "types":
                string str="";
                foreach(string key in SystemTypes.Keys) {
                    str+=key+" ";
                }
                Echo(str);
                break;
            case "add":
                MakeSystem maker;
                if(SystemTypes.TryGetValue(args[1],out maker)) {
                    var subargs = new string[args.Length-1];
                    for (var i=1;i<args.Length;i++) {
                        subargs[i-1]=args[i];
                    }
                    if(!Systems.ContainsKey(args[2])) {
                        Systems.Add(args[2],maker(this,subargs));
                    } else {
                        Echo($"System named {args[2]} already exists; del it first, or use replace");
                    }
                } else {
                    Echo($"Attempt to add unknown system type '{args[1]}'");
                }
                break;
            case "del":
                System sys;
                if(Systems.TryGetValue(args[1],out sys)) {
                    Systems.Remove(args[1]);
                } else {
                    Echo($"Attempt to delete unknown system '{args[1]}'");
                }
                break;
            }
        }
    } else {
        while(nextAction!=null && nextAction.when < runTime) {
            var action=nextAction;
            nextAction=action.next;
            RunAction(action);
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
public class Pinger : System {
    public Pinger(Program program, string name)
        : base(program, name, "Pinger") { }
    public override string StorageStr() {
        return $"pinger,{Name}";
    }
    public IEnumerator<double> DoPing() {
        program.screen.WriteText("Ping!",true);
        yield return 300;
        program.screen.WriteText("Pong!",true);
        yield return 0;
    }
    public override IEnumerator<double> HandleCommand(string[] args) {
        if (args[0]!=Name) {
            program.Echo($"pinger['{Name}']:HandleCommand called with command for {args[0]}[{args[1]}]");
            return null;
        }
        return DoPing();
    }
}
public static System MakePinger(Program program, string[] args) {
    if(args.Length!=2) {
        program.Echo($"Error: MakePinger given {args.Length} args!");
        return null;
    }
    return new Pinger(program, args[1]);
}
public enum AirlockState {
    Invalid,
    Pressurized,
    Depressurized,
    Cycling
}
public class Airlock : System {
    IMyAirVent vent;
    IMyDoor innerDoor, outerDoor;
    AirlockState state;
    public Airlock(Program program, string name, string ventName, string innerDoorName, string outerDoorName)
        : base(program, name, "Airlock") {
        vent = program.GridTerminalSystem.GetBlockWithName(ventName) as IMyAirVent;
        innerDoor = program.GridTerminalSystem.GetBlockWithName(innerDoorName) as IMyDoor;
        outerDoor = program.GridTerminalSystem.GetBlockWithName(outerDoorName) as IMyDoor;
        program.Echo($"airlock {name} {ventName} {innerDoorName} {outerDoorName}");
        if (vent==null || innerDoor==null || outerDoor==null) {
            program.Echo("Fail.");
            state=AirlockState.Invalid;
        } else {
            switch(vent.Status) {
                case VentStatus.Pressurizing:
                case VentStatus.Pressurized:
                    innerDoor.Enabled=true;
                    outerDoor.Enabled=false;
                    state=AirlockState.Pressurized;
                    break;
                case VentStatus.Depressurizing:
                case VentStatus.Depressurized:
                    innerDoor.Enabled=false;
                    outerDoor.Enabled=true;
                    state=AirlockState.Depressurized;
                    break;
            }
        }
    }

    public IEnumerator<double> PressurizeAirlock() {
        state=AirlockState.Cycling;
        while(outerDoor.Status!=DoorStatus.Closed) {
        outerDoor.Enabled=true;
        outerDoor.CloseDoor();
            yield return 10;
        }
        outerDoor.Enabled=false;
        vent.Depressurize=false;
        innerDoor.Enabled=true;
        innerDoor.OpenDoor();
        state=AirlockState.Pressurized;
        yield return 0;
    }
    public IEnumerator<double> DepressurizeAirlock() {
        state=AirlockState.Cycling;
        while(innerDoor.Status!=DoorStatus.Closed) {
            innerDoor.Enabled=true;
            innerDoor.CloseDoor();
                yield return 10;
        }
        innerDoor.Enabled=false;
        vent.Depressurize=true;
        yield return 120;
        outerDoor.Enabled=true;
        outerDoor.OpenDoor();
        state=AirlockState.Depressurized;
        yield return 0;
    }
    public override string StorageStr() {
        if(state==AirlockState.Invalid) {
            program.Echo("Airlock is invalid, saving empty string");
            return "";
        } else {
            return String.Join(",","Airlock",Name,vent.CustomName,innerDoor.CustomName,outerDoor.CustomName);
        }
    }
    public override IEnumerator<double> HandleCommand(string[] args) {
        if(args.Length>2) {
            program.Echo("Too many arguments in call to airlock");
            return null;
        }
        if(state==AirlockState.Invalid) {
            return null;
        }
        if(state==AirlockState.Cycling) {
            return null;
        }
        var cmd=args.Length==2 ? args[1] : "";
        switch (cmd) {
        case "pressurize":
            return PressurizeAirlock();
        case "depressurize":
            return DepressurizeAirlock();
        default:
            if(state==AirlockState.Depressurized) {
                return PressurizeAirlock();
            } else if(state==AirlockState.Pressurized) {
                return DepressurizeAirlock();
            }
            return null;
        }
    }
}
public static System MakeAirlock(Program program, string[] args) {
    if(args.Length!=5) {
        program.Echo($"Error: MakeAirlock given {args.Length} args!");
        return null;
    }
    return new Airlock(program, args[1],args[2],args[3],args[4]);
}

public abstract class MotorControl {
    public float min, max, velocity;
    public void SetTarget(float targetVal, float speed=0.1f) {
        var curVal=GetCurrent();
        var dif=targetVal-curVal;
        if (dif==0) {
            min=max=curVal;
            velocity=0f;
        } else if(dif>0) {
                min=curVal;
                max=targetVal;
                velocity=speed;
        } else {
                min=targetVal;
                max=curVal;
                velocity=-speed;
        }
    }
    public float TicksToExecute() {
        return 60f*(max-min)/velocity;
    }
    public float TicksToComplete() {
        var cur=GetCurrent();
        float dif=(velocity>0)?(max-cur):(cur-min);
        if(Math.Abs(dif)<.001) return 0;
        return 60f*dif/Math.Abs(velocity);
    }
    public void SetSpeedToCompleteIn(float ticks) {
        if(max-min==0) {
            velocity=0f;
        }
        else {
            var speed=(max-min)*60f/ticks;
            velocity=velocity/(float)Math.Abs(velocity)*speed;
        }
    }
    public abstract float GetCurrent();
}
public class HingeControl : MotorControl {
    public IMyMotorAdvancedStator Hinge;
    public override float GetCurrent() {
        if (Hinge==null) return 0;
        return Hinge.Angle;
    }
    public  void Apply() {
        if(Hinge!=null) {
            Hinge.LowerLimitRad=min;
            Hinge.UpperLimitRad=max;
            Hinge.TargetVelocityRad=velocity;
        }
    }
    public  void Stop() {
        var cur=Hinge.Angle;
        Hinge.LowerLimitRad=cur;
        Hinge.UpperLimitRad=cur;
        Hinge.TargetVelocityRad=0;
    }
}
public class PistonControl : MotorControl {
    public IMyExtendedPistonBase Piston;
    public override float GetCurrent() {
        if (Piston==null) return 0;
        return Piston.CurrentPosition;
    }
    public  void Apply() {
        if(Piston!=null) {
            Piston.MinLimit=min;
            Piston.MaxLimit=max;
            Piston.Velocity=velocity;
        }
    }
    public  void Stop() {
        var cur=Piston.CurrentPosition;
        Piston.MinLimit=cur;
        Piston.MaxLimit=cur;
        Piston.Velocity=0;
    }
}
public class Arm : System {
    string BaseHingeName, PistonName, EndHingeName, ConnectorName;
    HingeControl BaseHinge;
    HingeControl EndHinge;
    PistonControl Piston;
    IMyShipConnector Connector;
    public Arm(Program program, string name, string baseHingeName, string pistonName, string endHingeName, string connectorName)
        : base(program, name, "Arm") {
            BaseHingeName=baseHingeName;
            PistonName=pistonName;
            EndHingeName=endHingeName;
            ConnectorName=connectorName;
            BaseHinge=new HingeControl();
            EndHinge=new HingeControl();
            Piston=new PistonControl();
            GetBlocks();
        }
    public override string StorageStr() {
        return $"Arm,{Name},{BaseHingeName},{PistonName},{EndHingeName},{ConnectorName}";
    }
    public void CurrentXY(out float X, out float Y) {
        var dist=7.5+Piston.GetCurrent();
        var angle=-BaseHinge.GetCurrent();
        X=(float)(dist*Math.Cos(angle));
        Y=(float)(dist*Math.Sin(angle));
    }
    public IEnumerator<double> MoveXY(float dx, float dy) {
        float X,Y;
        CurrentXY(out X,out Y);
        return MoveToXY(X+dx,Y+dy);
    }
    public IEnumerator<double> MoveToXY(float x, float y) {
        
        var angle=Math.Atan2(y,x);
        if (angle<0 || angle>90) {
            Log("MoveToXY: Angle out of bounds");
            return null;
        }
        angle=-angle;
        var dist=Math.Sqrt(x*x+y*y);
        if (dist<7.5 || dist>17.5) {
            Log("MoveToXY: Distance out of bounds!");
            return null;
        }

        Log($"MoveToXY: {x},{y} => {dist}m @ {angle} rads");
        
        var angleD=angle-BaseHinge.GetCurrent();
        return MoveTo((float)angle,(float)dist-7.5f,(float)(EndHinge.GetCurrent()-angleD));
    }
    public IEnumerator<double> MoveTo(float baseTarget, float pistonTarget, float endTarget) {
        if(!HasValidBlocks()) {
            Log("MoveTo - fail, missing some blocks");
            yield return 0.0;
        }
        BaseHinge.SetTarget(baseTarget,0.5f);
        EndHinge.SetTarget(endTarget,0.5f);
        Piston.SetTarget(pistonTarget,0.1f);
        BaseHinge.SetSpeedToCompleteIn(300f);
        EndHinge.SetSpeedToCompleteIn(300f);
        Piston.SetSpeedToCompleteIn(300f);
        BaseHinge.Apply();
        EndHinge.Apply();
        Piston.Apply();
        double ticks=300;
        double timeLimit=ticks*2;
        while(ticks>1) {
            yield return ticks+6;
            timeLimit-=ticks;
            if(timeLimit<0) {
                Log("MoveTo timed out");
                break;
            }
            ticks=Math.Max(Math.Max(BaseHinge.TicksToComplete(),EndHinge.TicksToComplete()),Piston.TicksToComplete());
            Log($"MoveTo yielding for {ticks} more ticks");
        }
        BaseHinge.Stop();
        EndHinge.Stop();
        Piston.Stop();
        Log("MoveTo stopping");
        yield return 0;
    }

    public override IEnumerator<double> HandleCommand(string[] args) {
        if (args[0]!=Name) {
            Log($"HandleCommand called with command for {args[0]}");
            return null;
        }
        switch(args[1]) {
            case "MoveTo": {
                if(args.Length!=5) {
                    Log($"MoveTo called with invalid args - got {args.Length-2}, expected 3");
                    return null;
                }
                double[] argsDouble={0.0,0.0,0.0};
                for (var i=0;i<3;i++) {
                    if (!Double.TryParse(args[i+2],out argsDouble[i])) {
                        Log($"MoveTo called with invalid arg {i+1} - expected double");
                        return null;
                    }
                }
                return MoveTo((float)argsDouble[0]*(float)Math.PI/180.0f,(float)argsDouble[1],(float)argsDouble[2]*(float)Math.PI/180.0f);
            }
            case "MoveToXY": {
                if(args.Length!=4) {
                    Log($"MoveToXY called with invalid args - got {args.Length-2}, expected 2");
                    return null;
                }
                double[] argsDouble={0.0,0.0};
                for (var i=0;i<2;i++) {
                    if(!Double.TryParse(args[i+2],out argsDouble[i])) {
                        Log($"MoveToXY called with invalid arg {i+1} - expected double");
                        return null;
                    }
                }
                return MoveToXY((float)argsDouble[0],(float)argsDouble[1]);
            }
            case "MoveXY":{
                if(args.Length!=4) {
                    Log($"MoveXY called with invalid args - got {args.Length-2}, expected 2");
                    return null;
                }
                double[] argsDouble={0.0,0.0};
                for (var i=0;i<2;i++) {
                    if(!Double.TryParse(args[i+2],out argsDouble[i])) {
                        Log($"MoveXY called with invalid arg {i+1} - expected double");
                        return null;
                    }
                }
                return MoveXY((float)argsDouble[0],(float)argsDouble[1]);
            }
            case "Check":{
                float X,Y;
                CurrentXY(out X,out Y);
                Log($"Current position: X={X},Y={Y}");
                return null;
            }
            default:
                Log("Wha?");
                break;
        }
        return null;
    }
    public bool HasValidBlocks() {
        return !(
                BaseHinge.Hinge==null ||
                Piston.Piston==null ||
                EndHinge.Hinge==null ||
                Connector==null
        );
    }
    public bool GetBlocks() {
        BaseHinge.Hinge=program.GridTerminalSystem.GetBlockWithName(BaseHingeName) as IMyMotorAdvancedStator;
        Piston.Piston=program.GridTerminalSystem.GetBlockWithName(PistonName) as IMyExtendedPistonBase;
        EndHinge.Hinge=program.GridTerminalSystem.GetBlockWithName(EndHingeName) as IMyMotorAdvancedStator;
        Connector = program.GridTerminalSystem.GetBlockWithName(ConnectorName) as IMyShipConnector;
        return HasValidBlocks();
    }
}
public static System MakeArm(Program program, string[] args) {
    if(args.Length!=6) {
        program.Echo($"MakeArm given {args.Length} args!");
        return null;
    }
    return new Arm(program, args[1],args[2],args[3],args[4],args[5]);
}
public Dictionary<string,MakeSystem> SystemTypes=new Dictionary<string,MakeSystem>{
	{"Pinger", MakePinger},
	{"Airlock", MakeAirlock},
	{"Arm", MakeArm},
};
