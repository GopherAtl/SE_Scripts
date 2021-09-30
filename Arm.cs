namespace SEScripts {

class ArmSystem {

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
//    public abstract void Apply();
//    public abstract void Stop();
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
    public /*override*/ void Stop() {
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

    public /*override*/ void Apply() {
        if(Piston!=null) {
            Piston.MinLimit=min;
            Piston.MaxLimit=max;
            Piston.Velocity=velocity;
        }
    }
    public /*override*/ void Stop() {
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
        return $"Arm,{Name},{BaseHingeName},{PistonName},{EndHingeName},{ConnectorName}"; //TODO: the thing
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
        //TODO: would be pretty smexy if this were, y'know, general. Even a 
        // little bit general. For now it is pretty explicitly coded for the
        // initial hinge-piston-hinge arrangement I'm using at time of 
        // coding.
        
        //figure out the target angle for the top hinge to achieve this dx,dy
        var angle=Math.Atan2(y,x);

        if (angle<0 || angle>90) {
            Log("MoveToXY: Angle out of bounds");
            return null;
        }
        angle=-angle; 

        //figure out the piston extension
        var dist=Math.Sqrt(x*x+y*y);
        if (dist<7.5 || dist>17.5) {
            Log("MoveToXY: Distance out of bounds!");
            return null;
        }        
        

        Log($"MoveToXY: {x},{y} => {dist}m @ {angle} rads");
        
        //change in the base hinge angle - end hinge will move equal opposite
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
        //this check *shouldn't* be needed, we shouldn't be
        //called until these have been checked, but for sanity
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

}
}