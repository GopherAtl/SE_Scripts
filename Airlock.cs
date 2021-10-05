namespace SEScripts {

class AirlockSystem {
public enum AirlockState {
    Invalid,
    Pressurized,
    Depressurized,
    Cycling
}


public class Airlock : System {
    string innerDoorName, outerDoorName, ventName;
    IMyAirVent vent;
    List<IMyDoor> innerDoors, outerDoors;
    AirlockState state;

    public Airlock(Program program, string name, string ventName, string innerDoorName, string outerDoorName)
        : base(program, name, "Airlock") {
        this.innerDoorName=innerDoorName;
        this.outerDoorName=outerDoorName;
        this.ventName=ventName;

        vent = program.GridTerminalSystem.GetBlockWithName(ventName) as IMyAirVent;
        innerDoors=new List<IMyDoor>();
        outerDoors=new List<IMyDoor>();
        GetBlocks<IMyDoor>(innerDoorName,innerDoors);
        GetBlocks<IMyDoor>(outerDoorName,outerDoors);
        program.Echo($"airlock {name} {ventName} {innerDoorName} {outerDoorName}");
        if (vent==null || innerDoors.Count==0 || outerDoors.Count==0) {
            program.Echo("Fail.");
            state=AirlockState.Invalid;
        } else {
            switch(vent.Status) {
                case VentStatus.Pressurizing:
                case VentStatus.Pressurized:
                    foreach (var door in innerDoors) door.Enabled=true;
                    foreach (var door in outerDoors) door.Enabled=false;
                    state=AirlockState.Pressurized;
                    break;
                case VentStatus.Depressurizing:
                case VentStatus.Depressurized:
                    foreach (var door in innerDoors) door.Enabled=false;
                    foreach (var door in outerDoors) door.Enabled=true;
                    state=AirlockState.Depressurized;
                    break;
            }
        }

    }

    public void GetBlocks<T>(string name,List<T> list) where T: class, IMyTerminalBlock  {

        var g=program.GridTerminalSystem.GetBlockGroupWithName(name);
        if(g!=null) {
            g.GetBlocksOfType(list);
        } else {
            //no group, blocks
            program.GridTerminalSystem.GetBlocksOfType(list, block=>block.CustomName==name);
        }
    }

    public IEnumerator<double> PressurizeAirlock() {
        state=AirlockState.Cycling;

        while(true) {
            var closed=true;
            foreach(var door in outerDoors) closed=closed&&(door.Status==DoorStatus.Closed);
            if(closed) break;
            foreach(var door in outerDoors) {
                door.Enabled=true;
                door.CloseDoor();
            }
            yield return 10;
        }
        foreach (var door in outerDoors) door.Enabled=false;

        vent.Depressurize=false;
        foreach (var door in innerDoors) {
            door.Enabled=true;
            door.OpenDoor();
        }
        state=AirlockState.Pressurized;
        yield return 0;
    }

    public IEnumerator<double> DepressurizeAirlock() {
        state=AirlockState.Cycling;
        while(true) {
            var closed=true;
            foreach (var door in innerDoors) closed=closed&&(door.Status==DoorStatus.Closed);
            if(closed) break;
            foreach (var door in innerDoors) {
                door.Enabled=true;
                door.CloseDoor();
            }
            yield return 10;
        }
        foreach(var door in innerDoors) door.Enabled=false;
        vent.Depressurize=true;
        //give it time to pull the air out
        yield return 120;
        foreach(var door in outerDoors) {
            door.Enabled=true;
            door.OpenDoor();
        }
        state=AirlockState.Depressurized;
        yield return 0;
    }

    public override string StorageStr() {
        if(state==AirlockState.Invalid) {
            program.Echo("Airlock is invalid, saving empty string");
            return "";
        } else {
            return String.Join(",","Airlock",Name,ventName,innerDoorName,outerDoorName);
        }
    }

    public override IEnumerator<double> HandleCommand(ArgParser args) {

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

        switch (args.function) {
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

public static System MakeAirlock(Program program, string name,MyIni ini) {
    
    var vent=ini.Get(name,"vent");
    var innerDoor=ini.Get(name,"innerDoors");
    var outerDoor=ini.Get(name,"outerDoors");
    
    return new Airlock(program, name,vent.ToString(),innerDoor.ToString(),outerDoor.ToString());
}

// add airlock Airlock_Dock Airlock_Dock_Vent Airlock_Dock_Inner_Door Airlock_Dock_Outer_Door
}
}