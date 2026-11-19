# Authentication + Pavilion Lobby (VR Cricket)

**Status: code only, NOT Play-tested in Unity.** This PR adds 3 scripts and
this README. It modifies no existing file, so rollback is deleting the
branch. The user (or a collaborator) merges after a Play test.

## What was added

| File | What it does |
|---|---|
| `Assets/scripts/Auth/PlayerAuthManager.cs` | Local profiles: create / sign in with name + 4-digit PIN (SHA-256 hash only, never plain text), Guest mode, save to `persistentDataPath/vr_cricket_profiles.json` (separate from GameManager's save). Syncs `GameManager.PlayerName` when a GameManager exists. |
| `Assets/scripts/Lobby/VRLobbyManager.cs` | Auth panel -> Pavilion lobby panel -> Match / Nets / Training info / How to Play. Builds a WorldSpace canvas + a preview-matched pavilion dressing room at runtime (glass wall + floodlit stadium backdrop, detailed lockers, sofa, team-talk board, wooden floor, bench, ceiling lights). Still code primitives only — no model files, no scene edits needed. |
| `Assets/scripts/Lobby/AuthLobbyBootstrap.cs` | Auto-creates the lobby on scene load (same pattern as `CricketMatchBootstrap`). If you place `VRLobbyManager` in the scene by hand, the bootstrap does nothing. |

Lobby choice used: **Option A — Pavilion** (the recommended option from the
UI preview). Modes are labelled as places: "Walk Out: Match vs AI Bowler",
"Go to Nets: Practice", "Training Room", "How to Play", "Sign Out".

## Upgrade note (Oct 29, after user feedback)

The first version of this PR used a plain cube shell (floor, walls,
lockers, bench). The user pointed out that the preview showed a much
richer pavilion, so the shell was upgraded to match the preview
layout with code primitives only: glass wall, stadium backdrop,
floodlights, detailed lockers, sofa, team-talk board, wooden floor
and ceiling lights. No external models were added.


## Fix note (Oct 30, after Play screenshot — pavilion was ON the pitch)

The first merged version anchored the pavilion to the player's start
position, and the player starts at the batsman — so the dressing room,
its floor and its ceiling landed on top of the pitch, over the stumps,
and its fake field/pitch strip painted a second ground over the real
one. That was the mess in the screenshot.

Fix (branch `fix/pavilion-off-pitch`, unmerged):
- Pavilion now sits `pavilionSideDistance` (default 30 m) to the SIDE of
  the pitch centre, perpendicular to the pitch direction, facing back
  toward the pitch — where a real ground's pavilion sits, at the
  boundary edge. Tune the distance in the Inspector if it lands inside
  the stands in your scene scale; `pavilionOnRightSide` flips the side.
- Player rig is teleported into the pavilion while the lobby is open,
  and teleported back to the saved batsman position on Walk Out (and
  on Resume from Esc-pause). Esc from a match therefore also walks you
  back into the pavilion, it does not just show a panel 30 m away.
- Fake StadiumField / StadiumPitch / floodlights are deleted. The
  glass wall now looks onto the real SampleScene ground.
- Ground is y = 0 (the old code used the XR Origin height as ground,
  which floated the floor). XR Origin lookup now matches the scene
  name "XR Origin (VR)".
- Still NOT Unity-compiled/Play-tested by the agent. Test with the
  default camera first: camera switching (keys 1-9) during a match
  changes Camera.main, so Esc teleport may not move a switched camera.

## How to test (Unity 6000.6.6f1, pull the PR branch, open SampleScene, Play)

1. **Auth first, no match yet.** The Auth panel must appear and the bowler
   must NOT bowl yet. If the bowler bowls behind the panel, the bootstrap
   order is broken — report it, do not merge.
2. **Guest:** click Guest -> lobby shows "Welcome, Guest" -> click
   "Walk Out: Match" -> pavilion + lobby hide, bowler bowls.
3. **New player:** New Player tab -> name (2-24 chars) + 4-digit PIN ->
   Create Player -> lobby shows your name. Stop Play, Play again, sign in
   with the same name + PIN -> must succeed. Wrong PIN -> must show
   "Wrong PIN" and stay on Auth. Same name twice -> must refuse.
4. **Lobby return:** during a match press **Esc** -> lobby returns and the
   game pauses. Click "Walk Out: Match" again -> game resumes (it does
   NOT restart the over — return-to-lobby is a pause, see below).
5. **Training button:** must show the info panel explaining
   ModuleSelectorUI is not in SampleScene. It must NOT pretend training
   started.
6. **Sign Out:** from lobby, Sign Out -> Auth panel again, game unpaused.

## Honest limits (do not present these as done)

- **No Google sign-in, no server account.** The repo has no auth backend.
  Google is in the preview as a future option only. This PR is local
  profiles + Guest.
- **VR typing:** name/PIN typing needs a physical keyboard on PC. On
  Quest there is no XR keyboard in this repo yet, so in VR use Guest.
- **VR button clicks:** buttons need XR controller interactors in the
  scene (still an open editor step from the Oct 29 audit). On PC,
  mouse clicks work via the existing EventSystem.
- **Pavilion art:** the shell is code primitives only (glass pane,
  floodlights, lockers, sofa, board, bench, lights) — it matches the
  preview layout, but it is not a finished 3D art pass with real
  pavilion models. The lockers, glass door and board are visual
  anchors, not clickable objects: the panel buttons are the controls
  until XR controller interactors exist in the scene. A real art pass
  can replace the shell by setting `buildPavilionShell = false` on
  `VRLobbyManager` and parenting the canvas under a real room
  (`pavilionAnchor`).
- **Match vs Nets:** both start the same bowling loop and physics today.
  Match records a session on the profile; Nets does not. There is no
  separate nets physics mode yet.
- **Return-to-lobby is a pause, not a reset.** Deactivating bowlers
  mid-match would strand `BowlingManager.waitingForNextBall` (private,
  coroutine killed on deactivate) and can stall the next delivery, so
  this PR deliberately pauses with `Time.timeScale = 0` instead. A full
  reset is a scene reload, which returns to the Auth panel.
- **Not Unity-compiled by the agent.** The agent runs in a Linux
  sandbox with no Unity. Braces and API use were checked by reading
  the code, but the user must confirm zero console errors on Play
  before merging.

## Rollback

Delete the branch / close the PR. No existing file was changed, so main
is untouched.
