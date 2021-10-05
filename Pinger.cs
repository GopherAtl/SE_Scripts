namespace SEScripts {

class PingerSystem {
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

    public override IEnumerator<double> HandleCommand(ArgParser args) {

        return DoPing();
    }
}

public static System MakePinger(Program program, string name, MyIni ini) {
    return new Pinger(program, name);
}
}
}