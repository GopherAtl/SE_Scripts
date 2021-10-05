namespace SEScripts {

class LifeSupportSystem {
public class LifeSupport : System {

    public LifeSupport(Program program, string name)
        : base(program, name, "LifeSupport") { }


    public IEnumerator<double> Depressurize() {
        yield return 0;
    }

    public IEnumerator<double> Pressurize() {
        yield return 0;
    }

    public override IEnumerator<double> HandleCommand(ArgParser args) {

        return null;
    }
}

public static System MakeLifeSupport(Program program, string name, MyIni ini) {
    return new LifeSupport(program, name);
}
}
}