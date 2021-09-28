GopherAtl's scripts for SpaceEngineers.

baseQueue.cs has the foundation, which doesn't do anything by itself - actually it won't even run by itself probably, since it references an undefined dictionary.

It handles a queue of tasks, ordered by time until they are due to run again. 

Tasks are created by subclasses of System, which run as IEnumerator-based coroutines.

You're free to do whatever you want with this code - steal it, modify it, whatever. I don't actually recommend using it, partly because I make no committment to providing tech support, and partly because I'm just not writing it with any eye towards distribution or use by people-who-aren't-me, so it's poorly documented and at any given time may or may even be functional as-is. Fair warning!