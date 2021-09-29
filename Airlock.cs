namespace SEScripts {

class AirlockSystem {
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
        : base(program, name) {

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
        //give it time to pull the air out
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
            //TODO: attempt again to validate maybe? Would require me to 
            // have the Airlock itself remember the *names* of it's 
            // blocks
            return null;
        }
        
        if(state==AirlockState.Cycling) {
            //gotta wait for now. TODO maybe allow override/reverse?
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

// add airlock Airlock_Dock Airlock_Dock_Vent Airlock_Dock_Inner_Door Airlock_Dock_Outer_Door
}
}