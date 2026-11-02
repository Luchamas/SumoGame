# Sumo Game — Specification

Version 2.1

This spec describes the game as built, the reference it is modelled on, and what is still unknown. It replaces version 1.0 and its clarifications. Where the two differ, this version is correct.

Values marked **Reference** were observed or read from sources. Values marked **Chosen** are design decisions, and may differ from the reference on purpose. All tuning values live in `SumoRules` (`Assets/Scripts/Sim/SumoSim.cs`) and `BotProfile` (`Assets/Scripts/Sim/SumoBot.cs`).

## 1. Overview

A 1v1 sumo game modelled on the sumo minigame in *The Legend of Zelda: Twilight Princess*. The goal is to drive the opponent over the rope of a circular ring.

The fight is rock-paper-scissors with weight. Each action is a commitment, the ring position matters, and a counter earns an opening rather than a win.

- **Singleplayer:** a run through ten rivals of rising difficulty.
- **Versus:** two players on one keyboard, with a vertical split screen.

## 2. Reference study

### 2.1 Sources

| Source | Used for |
|---|---|
| [Link vs Gor Coron - Sumo Fight](https://www.youtube.com/watch?v=2TSFy_wXc1A) (30 fps) | Frame study, Gor Coron fight |
| [Sumo 101 with the Mayor of Ordon Village](https://www.youtube.com/watch?v=1bUbnvlKHD4) (29.97 fps) | Frame study, Mayor Bo rounds |
| [The Way of Sumo, Part II](https://zeldawiki.wiki/wiki/The_Way_of_Sumo,_Part_II) | In-game poster: the three counters |
| Zelda Dungeon walkthrough, Zelda Wiki and game FAQ excerpts | Controls, tactics, sidestep direction, slap-only wins |
| [Gameswelt walkthrough](https://www.gameswelt.de/the-legend-of-zelda-twilight-princess/komplettloesung/the-legend-of-zelda-twilight-princess-1902/9) | Circling back to the centre |

Timings were read from frame strips at 2 to 7 frames per second, so treat them as accurate to about 0.2 seconds. Times below are positions in each video.

### 2.2 Controls in the reference

- Left and Right move sideways. There is no separate dodge input.
- A grabs, and is pressed again to continue a grab or resist one.
- B slaps.
- There is no forward or backward control.

Guides describe the wrestlers' options as grab, slap and sidestep. Grab beats slap "by dodging in place", slap beats sidestep, and sidestep beats grab.

### 2.3 Measured from footage

| Observation | Footage | Value |
|---|---|---|
| "READY?" to "FIGHT!" | Bo 31.5 to 33.5 s, Gor 89.5 to 91.5 s | About 2 s |
| Starting distance | Both fights | Arm's length, no walk-in |
| Round length | Bo 33.5 to 42.5 s and 60.5 to 79 s, Gor 91.75 to 121.5 s | About 9, 18 and 30 s |
| Clinch, arms wrapped | Gor 93.0 to 95.9 s and 103.9 to 107.0 s, Bo 67.5 to 70.75 s | 2.9, 3.1 and 3.2 s |
| Arms-wide pose | Gor 98.75 to 100.5 s, 109.25 to 111 s, 119.5 to 121 s; Bo 36.5 to 37.5 s, 72.25 to 74 s, 76.66 to 78.33 s | 1.0 to 1.75 s |
| Break to next neutral after a clinch | Gor 95.9 to 96.4 s, 107.0 to 107.75 s | 0.5 to 0.75 s |
| Sidestep past a charge | Gor 102.87 to 103.6 s | About 0.7 s |
| Grab landing after that miss | Gor 103.72 s | About 0.1 s after the miss ends |

### 2.4 Other observations

- **Neutral is brief.** Left and right arrows plus the Slap and Grab prompts show only while both wrestlers are neutral, at Gor 91.5, 96.4, 97.56, 102.87 and 107.75 s. Most of a fight is spent inside committed animations.
- **Prompts are contextual.** Between neutrals the screen shows only what applies, such as "Grab" alone or "Slap" alone.
- **A missed charge is obvious.** Gor Coron carries on past Link with his side turned, then Link grabs him while he recovers.
- **The ground moves.** Floor markings keep shifting through a clinch, so a contested push still gains ground.
- **The camera is low and close.** It sits just above and behind Link, and orbits to stay behind him as the pair rotate.
- **Opponents are far bigger than Link.** Their upper bodies stay visible over his head, which is how the reference avoids occlusion.
- **Rounds end from the arms-wide pose.** Gor at 119.5 to 121.5 s and Bo at 76.66 to 79 s both go into that pose and then over the edge. The loser lands flat on their back outside the ring.
- **A white spark marks contact**, at Bo 67.5 and 76.33 s.

### 2.5 Not established

- **What the arms-wide pose is.** It looks like the opponent being driven backward with the player in control, while the arms-wrapped clinch may be the other way round. Button prompts do not settle it. The game treats it as the finishing drive in section 6.4.
- **The exact cooldown between pushes.** No source states it. The attacker cooldown exists because the project owner remembers it from the game.
- **Slap against sidestep.** The Zelda Wiki excerpt says a sidestep avoids being struck, while the poster says slap beats sidestep. The poster is followed here.

## 3. Controls

| Action | P1 | P2 | Gamepad |
|---|---|---|---|
| Walk sideways | A / D | J / L | Left stick |
| Grab, push or resist | Space | I | South button |
| Strike | Left Shift | K | West button |
| Pause | Esc | Esc | Start |
| Confirm menus | Enter | Enter | South button |

- Keys are physical positions, so other keyboard layouts keep the same shape.
- Controller rumble plays during a grab.
- The menu also works with the mouse.
- Space does not confirm menus.

## 4. Movement

There is no forward, backward or dedicated dodge control.

- A and D walk sideways, relative to the opponent, so the wrestler circles them.
- Wrestlers always turn to face each other, at no more than 540° per second, so turning never snaps.
- Only the wrestler nearer the centre walks in to fighting distance. Ground lost to a shove or strike stays lost until the wrestler circles back. A 0.1 m tie lets both walk in.
- Walking cannot cross the rope. Only being shoved can.
- Movement is disabled during grabs, stuns, strikes and recovery.

| Value | Setting | Basis |
|---|---|---|
| Ring radius | 4.2 m | Chosen |
| Fighting and start distance | 1.75 m | Chosen. The reference is closer, but looks cramped with same-size wrestlers |
| Sideways speed | 2.2 m/s | Chosen |

## 5. Counters

| Action | Beats | Loses to |
|---|---|---|
| Walking sideways | Grab | Strike |
| Grab | Strike | Walking sideways |
| Strike | Walking sideways | Grab |

- **Walking sideways beats grab:** a grab misses if the target is walking sideways as it lands. There is no tap input and no dodge state.
- **Grab beats strike:** a wrestler in grab startup cannot be hit, and drives in low to duck the strike. A grab also catches a strike's recovery.
- **Strike beats walking sideways:** a strike reaches further against someone doing it.
- **Simultaneous actions:** two strikes stun both wrestlers, and two grabs clash and push both apart.

A counter earns an opening, not a win. The follow-up grab must still land and push.

## 6. Grabbing

### 6.1 Starting a grab

The grab starts within 2.3 m and lunges at 4 m/s for 0.25 seconds. The lunge stops where the bodies meet, 1.35 m apart. The defender is never moved by the grab landing, so a grab at the rope cannot throw them over it.

### 6.2 A missed grab

- The attacker keeps moving and stumbles past the target.
- They keep facing the way they lunged until recovery ends, then turn back.
- Recovery is 0.75 seconds, long enough to be punished.
- The stumble stops at the rope, so a dodge alone cannot win a round.

### 6.3 The struggle

A hidden meter starts at 50%. It is never drawn. The wrestlers' lean, the attacker's lunge on each shove, and controller rumble show who is winning.

- **Attacker:** each press is one shove, adding 22% and driving forward for 0.25 seconds. Then a 0.7 second cooldown runs, and presses during it do nothing. Spamming is not penalised, but it cannot go faster.
- **Defender:** each press removes 5%, once per 0.25 seconds. Extra presses wait for the next interval. One press is queued at most, so mashing never builds a backlog.
- **Resisting:** a shove during resistance keeps 60% of its speed. The defender can never reverse a push.
- **Steering:** the attacker can turn the push by up to 25° per second with A and D.

### 6.4 How a grab ends

| Outcome | Result |
|---|---|
| Meter reaches 100% | The finishing drive: 1.25 s in which nobody can act, the defender reels back with arms wide and the attacker follows. The defender is left stunned if still inside. The drive can carry them over the rope |
| Meter reaches 0%, after at least 0.5 s | The defender is free at once. The attacker recovers for 0.6 s and cannot grab or strike |
| 3 seconds pass | Both return to neutral |

The round ends only when a wrestler crosses the rope. An unresisted grab from the centre leaves the defender near the edge, still inside.

Wrestlers tire in long rounds. After 45 seconds, pushes carry 6% further per second, so every round ends. Normal rounds never reach this.

### 6.5 The attacker's cooldown

After any grab ends or misses, the attacker cannot grab again for 1.2 seconds. The defender has no such limit and can grab back at once. This is longer than the 0.5 to 0.75 second gap in the footage, on purpose, to stop grab spam.

## 7. Strikes and stun

| Property | Value |
|---|---|
| Startup | 0.2 s |
| Active window | 0.15 s |
| Recovery | 0.35 s |
| Reach | 2.0 m |
| Reach against a sidewalker | 2.8 m |
| Stun | 0.8 s |
| Immunity | 0.5 s, starting when the stun ends |
| Knockback | About 0.36 m |

- A stunned wrestler cannot move or act, but can be grabbed.
- A grab landing during a stun ends the stun, and immunity starts at that moment.
- During immunity a wrestler can still be hit and pushed, but not stunned.
- A strike beats the opponent back, so a round can be won without grabbing. This should be harder than grabbing.

### Feedback

- A soft spark bursts at the point of contact for strikes, grabs, clashes and the drive, as in the reference. Dust kicks up on shoves and ring-outs.
- Strikes freeze the fight for 0.07 seconds, and the drive and ring-outs for 0.1 seconds.
- Placeholder sounds are synthesized at start-up, for each impact, misses and the start of a fight.
- The loser falls flat on their back, as in the reference.

## 8. Camera

Both modes use the same camera, directly behind the wrestler with them centred on screen.

| Property | Value |
|---|---|
| Distance behind | 3.3 m |
| Height | 2.9 m |
| Look point | 1.3 m ahead, 1.1 m up |
| Field of view | 58° full screen, 66° split screen |

- The camera follows the wrestler's facing, so it orbits as they circle.
- It never zooms or swings to the side during a grab.
- The opponent's head must stay visible past the wrestler's own, in neutral and in a grab.
- Screen shake plays on a stun, and can be turned off.

This replaces the fixed overhead camera in version 1.0.

## 9. Match flow

1. **Intro, 4 seconds, first round of a match only.** Wide shot, the camera moves toward its gameplay position while the wrestlers step in, they face each other, then the camera lands and "HAKKEYOI..." appears. Input is disabled until it ends.
2. **Countdown, 1.2 seconds, later rounds.**
3. **Fight.** "NOKOTTA!" appears. A rival always takes a moment before its first move, so it never acts on the first instant of a round.
4. **Ring-out.** The round ends when a wrestler crosses the rope. The loser falls and the winner celebrates.
5. **Result, 2.4 seconds.** Confirm skips it. The next round starts automatically.

Matches are best of three.

## 10. Modes

### 10.1 Singleplayer

A run through ten rivals, each in a best-of-three match: Jonokuchi, Jonidan, Sandanme, Makushita, Juryo, Maegashira, Komusubi, Sekiwake, Ozeki, Yokozuna.

- Winning a match advances to the next rival.
- Losing a match ends the run.
- Beating Yokozuna completes the run.
- The best run is saved.
- Run progress is separate from match rules, so rounds and matches reset without affecting it.

### 10.2 Versus

- Vertical split screen, each player's camera behind their own wrestler.
- Rounds to win is set in Settings: 1, 2 or 3. The default is 2.

### 10.3 Menus

- **Title:** Singleplayer, Versus, Settings, Controls, Credits. Esc quits.
- **Settings:** Fullscreen, VSync, Camera shake, Volume, Versus rounds to win.
- **Pause:** Resume, Quit to title.

### 10.4 On-screen prompts

Prompts show only what can be pressed now, as in the reference. In neutral they show the movement keys, Grab and Strike, and Grab disappears during the cooldown. In a grab they show Push or Resist. Otherwise nothing is shown.

## 11. Rivals

Rivals use the same rules and inputs as players, with their own decision logic. Skill scales evenly from the first rival to the last.

| Value | First rival | Last rival |
|---|---|---|
| Reaction time | 0.42 s | 0.13 s |
| Odds of reading the right counter | 15% | 85% |
| Time between decisions | 1.5 s | 0.4 s |
| Grab presses per second | 1.5 | 3.9, rising quickly in the early ranks |
| Grab preference in range | 50% | 85% |
| Odds of stepping aside from a grab habit | 10% | 100%, reached by the fourth rival |

- **Reading habits:** after two repeated grabs, a rival may step aside early and punish the miss. A strike clears the read, and each miss means it needs fresh evidence.
- **Rope escape:** only skilled rivals reliably circle back off the rope. Weak rivals often stay there.
- **Sidestep direction:** skilled rivals sidestep toward the centre, so more of the arena is behind them.
- **Push steering:** skilled rivals aim pushes at the nearest rope.

All rivals currently look alike and have the same strength. Bigger, stronger enemies are planned, and the reference suggests strength should affect shoves and resisting. New enemy types should be added as `BotProfile` entries.

## 12. Technical

- The fight is a state machine in `SumoSim`, with no engine dependencies beyond math types, so it can be stepped from tests. States are Idle, Moving, GrabStartup, Recovering, Striking, Stunned, Pushing, Grabbed, Driving, Reeling, Falling and Victory.
- The simulation runs in fixed steps of 0.02 seconds. Input is latched between steps, so short presses are not lost.
- `SumoCamera` receives match state and positions, and owns both the scripted intro and the gameplay view.
- `SumoSim.Impact` reports hits, and `ImpactEffects` and `SumoAudio` respond to it, so effects never touch the rules.
- `WrestlerView` animates the placeholder puppet procedurally from sim state. Replacing the puppet with a rigged model means changing `WrestlerPuppetBuilder` and this class.
- `Assets/Scenes/Sumo.unity` is generated by `Sumo > Rebuild Scene`. Camera values are saved in the scene, so rebuild it after changing them.

### Balance checks

The automated tests must keep these true. Figures are from the current build.

| Check | Target | Current |
|---|---|---|
| Mirror matches | Neither side favoured | P2 wins 49% to 51% |
| Grab spam, wins of 100 by rank 0 to 3, then 4 and up | First two rivals allow it, then it fails | 100, 97, 34, 7, then 0 |
| Rival wins of 100 vs a mid-level bot, ranks 0 to 10 | Rising | 0, 9, 23, 60, 77, 81 |
| Rival wins of 100 vs a strong bot, ranks 6, 8, 10 | Rising | 30, 35, 53 |
| Matches that never finish | None | None at any rank. The longest spam match was 4 minutes |
| Finishing drives per match against grab spam | Common against weak rivals | About 5 to 7 up to rank 2, about 0.4 at rank 3, none above |

## 13. Known gaps and open questions

- **Finishing drive.** It is modelled on the arms-wide pose, whose real meaning is not established. Rivals from rank 3 up usually resist well enough to prevent it, including against timed shoves.
- **Rival size.** The reference's opponents are much bigger. Scaling was tried at 1.3× and 1.45×, then removed at the owner's request.
- **Distance.** 1.75 m is wider than the reference's arm's length.
- **Round length.** Reference rounds take 9 to 30 seconds. Best-of-three matches here take about 35 to 90 seconds, and a human-paced figure has not been measured.
- **Slap-only wins.** They work against a passive opponent, in about 11 seconds. A scripted strafe-and-strike player has not beaten a real rival.
- **Spam matches.** Pure spam still runs long at ranks 2 and 3, averaging 1.5 to 2 minutes.
- **Sound.** The placeholder sounds are checked as valid audio, but nobody has judged how they sound.
- **Hardware.** Physical controller input and rumble are unconfirmed.
- **Art.** The wrestlers are placeholder puppets built from primitives. The arena is "sumo arena" by unForastero (CC0, OpenGameArt).
