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


    public override IEnumerator<double> HandleCommand(ArgParser args) {
        switch(args.function) {
            case "MoveTo": {
                args.Expect(3);
                double h1a=args.NextDouble();
                double px=args.NextDouble();
                double h2a=args.NextDouble();

                if(args.error!="") {
                    Log(args.error);
                    return null;
                }
                return MoveTo((float)h1a*(float)Math.PI/180.0f,(float)px,(float)h2a*(float)Math.PI/180.0f);
            }
            case "MoveToXY": {
                args.Expect(2);
                var x=(float)args.NextDouble();
                var y=(float)args.NextDouble();
                if(args.error!="") {
                    Log(args.error);
                    return null;
                }
                return MoveToXY(x,y);
            }
            case "MoveXY":{
                args.Expect(2);
                var x=(float)args.NextDouble();
                var y=(float)args.NextDouble();
                if(args.error!="") {
                    Log(args.error);
                    return null;
                }
                return MoveXY(x,y);
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

public static System MakeArm(Program program, string name, MyIni ini) {
    
    var baseHinge=ini.Get(name,"BaseHinge");
    var endHinge=ini.Get(name,"EndHinge");
    var piston=ini.Get(name,"Piston");
    var connector=ini.Get(name,"Connector");

    return new Arm(program, name,baseHinge.ToString(),piston.ToString(),endHinge.ToString(),connector.ToString());
}

}
}