

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

    public abstract float GetCurrent();
//    public abstract void Apply();
}

public class HingeControl : MotorControl {
    public IMyMotorAdvancedStator Hinge;

    public override float GetCurrent() {
        if (Hinge==null) return 0;
        return Hinge.Angle;
    }

    public /*override*/ void Apply() {
        if(Hinge!=null) {
            Hinge.LowerLimitRad=min;
            Hinge.UpperLimitRad=max;
            Hinge.TargetVelocityRad=velocity;
        }
    }
}

public class PistonControl : MotorControl {
    public IMyExtendedPistonBase Piston;

    public override float GetCurrent() {
        if (Piston==null) return 0;
        return Piston.CurrentPosition;
    }

    public /*override*/ void Apply() {
        if(Piston!=null) {
            Piston.MinLimit=min;
            Piston.MaxLimit=max;
            Piston.Velocity=velocity;
        }
    }
}

public class Arm : System {

    string BaseHingeName, PistonName, EndHingeName, ConnectorName;

    HingeControl BaseHinge;
    HingeControl EndHinge;
    PistonControl Piston;
    IMyShipConnector Connector;

    public Arm(Program program, string name, string baseHingeName, string pistonName, string endHingeName, string connectorName) 
        : base(program, name) { 
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
        return $"Arm,{Name},{BaseHingeName},{PistonName},{EndHingeName},{ConnectorName}"; //TODO: the thing
    }

    public IEnumerator<double> MoveTo(float baseTarget, float pistonTarget, float endTarget) {
        if(!HasValidBlocks()) {
            program.Echo($"Arm['{Name}']:MoveTo - fail, missing some blocks");
            yield return 0.0;
        }
        
        BaseHinge.SetTarget(baseTarget*(float)Math.PI/180.0f,0.5f);
        EndHinge.SetTarget(endTarget*(float)Math.PI/180.0f,0.5f);
        Piston.SetTarget(pistonTarget,0.1f);
        BaseHinge.Apply();
        EndHinge.Apply();
        Piston.Apply();

        yield return 0.0;
    }
    

    public override IEnumerator<double> HandleCommand(string[] args) {
        //this check *shouldn't* be needed, we shouldn't be
        //called until these have been checked, but for sanity
        if (args[0]!=Name) { 
            program.Echo($"Arm['{Name}']:HandleCommand called with command for {args[0]}");
            return null;
        }

        switch(args[1]) {
            case "MoveTo":
                if(args.Length!=5) {
                    program.Echo($"Arm['{Name}'].MoveTo called with invalid args - got {args.Length-2}, expected 3");
                    return null;
                }
                double[] argsDouble={0.0,0.0,0.0};
                for (var i=0;i<3;i++) {
                    if (!Double.TryParse(args[i+2],out argsDouble[i])) {
                        program.Echo($"Arm['{Name}'].MoveTo called with invalid args - expected double");
                        return null;
                    }                    
                }
                return MoveTo((float)argsDouble[0],(float)argsDouble[1],(float)argsDouble[2]);
            default:
                program.Echo("Wha?");
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
        program.Echo($"Error: MakeArm given {args.Length} args!");
        return null;
    }
   
    return new Arm(program, args[1],args[2],args[3],args[4],args[5]);
}
