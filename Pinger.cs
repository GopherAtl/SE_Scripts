namespace SEScripts {

class PingerSystem {
public class Pinger : System {

    public Pinger(Program program, string name)  
        : base(program, name) { }


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
        //this check *shouldn't* be needed, we shouldn't be
        //called until these have been checked, but for sanity
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
}
}