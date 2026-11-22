# Super Blink

For RimWorld 1.7. Super Blink instantly moves pawns while preserving RimWorld's vanilla work, pickup, and reservation logic.

When a pawn has reserved a haulable item for its current job, it skips the trip to the item by completing the goto toil through the normal instant-toil flow. This avoids re-entering `JobDriver.TryActuallyStartNextToil` from inside the toil's own init action. The vanilla carry toil picks up the item first; only after the pawn is successfully carrying it does the pawn teleport to the item's former cell. The item is never teleported. Pawns also teleport to the end of vanilla paths that are at least five cells away, including while carrying materials to a work target. The original job then places the materials as usual.

Vanilla still chooses and reserves materials and performs the pickup. Forbidden items and items reserved by another pawn are not taken, and vanilla reachability rules are not bypassed. Teleporting general pawn movement removes normal travel time and may bypass movement-related interactions. Movement systems that do not use `Pawn_PathFollower` are not affected.

## 한국어

Super Blink은 림월드의 기본 작업, 물건 집기 및 예약 방식을 유지하면서 림의 이동만 순간이동시킵니다.

현재 작업을 위해 림이 운반 가능한 물건을 예약하면, goto toil을 바닐라의 Instant toil 완료 흐름으로 끝내 해당 물건까지 걷는 과정을 생략합니다. toil의 초기화 작업 안에서 JobDriver를 재진입시키지 않아 동일 틱의 재귀적인 작업 재시작을 방지합니다. 이후 바닐라 운반 toil이 물건을 정상적으로 집고, 림이 실제로 물건을 든 것이 확인된 다음에만 림을 물건이 있던 위치로 순간이동시킵니다. **물건 자체의 위치는 변경하지 않습니다.** 림은 5칸 이상 떨어진 바닐라 이동 경로의 목적지로도 순간이동하며, 재료를 든 채 작업 대상으로 이동할 때도 적용됩니다. 재료를 놓는 동작은 기존 작업 로직을 따릅니다.

물건의 선택, 예약 및 집기는 바닐라가 처리합니다. 금지된 물건이나 다른 림이 예약한 물건은 가져오지 않으며, 바닐라의 도달 가능성 검사도 우회하지 않습니다. 일반 이동에 순간이동을 적용하면 이동 시간이 사라지고 이동 중 상호작용이 생략될 수 있습니다. `Pawn_PathFollower`를 사용하지 않는 이동 방식에는 적용되지 않습니다.

Requires Harmony.

Run `Build.bat` to build the Release assembly into `1.7\Assemblies\InstantWorkSupply.dll`. Use `Build.bat /nopause` to suppress the final pause.
