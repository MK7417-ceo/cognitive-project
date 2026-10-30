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
