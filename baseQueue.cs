
public delegate System MakeSystem(Program program, string[] args);

abstract public class System {
    public string Name;
    public Program program;    
    public abstract string StorageStr();
    public abstract IEnumerator<double> HandleCommand(string[] args);
    
    public System(Program program, string name) {
        Name=name;
        this.program=program;
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
    
    //load

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
      //runTime+TimeSpan.FromSeconds(delay/60)
    if(action.func.Current!=0.0) {
        //if it's not done, queue it up to continue
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
        //commands begin with the name of the system being commanded.    
        System cmdTarget;
        
        if(Systems.TryGetValue(args[0],out cmdTarget)) {
            var handler=cmdTarget.HandleCommand(args);        
            if(handler!=null) {
                TimedAction newAction=new TimedAction(runTime,handler);
                RunAction(newAction);
            }
            
        } else {
            //TODO: maybe some core system commands? Might tie that into the 
            // regular system tho...        
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
                    //restructure args                    
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
            //pop next action
            var action=nextAction;
            nextAction=action.next;
            //do the thing
            RunAction(action);
            //TODO: monitor spent cycles, end early and let things 
            //run late if necessary? Feells unlikely to come up
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


