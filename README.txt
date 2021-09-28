GopherAtl's scripts for SpaceEngineers.

baseQueue.cs has the foundation, which doesn't do anything by itself - actually it won't even run by itself probably, since it references an undefined dictionary.

It handles a queue of tasks, ordered by time until they are due to run again. 

Tasks are created by subclasses of System, which run as IEnumerator-based coroutines.

You're free to do whatever you want with this code - steal it, modify it, whatever. I don't actually recommend using it, partly because I make no committment to providing tech support, and partly because I'm just not writing it with any eye towards distribution or use by people-who-aren't-me, so it's poorly documented and at any given time may or may even be functional as-is. Fair warning!

The separate scripts are meant to be concatenated into a single file, and the dictionary defined to associate the System subclasses with their respective Make functions (class Foo will always have a corresponding static method MakeFoo). The included build.go file automates this process, but if you're just wanting to poke it with a stick, you can just grab the output.cs file, which is the result generated ready for pasting into space engineers.
