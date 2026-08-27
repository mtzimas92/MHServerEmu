using MHServerEmu.Games.UI.Widgets;

namespace MHServerEmu.Games.MythicRifts
{
    public enum MythicRiftRunStatus
    {
        Pending,
        Active,
        Success,
        Failed,
        Aborted
    }

    public sealed class MythicRiftRunState
    {
        private readonly HashSet<ulong> _participantPlayerDbIds = new();
        private readonly HashSet<ulong> _admittedPlayerDbIds = new();
        private readonly HashSet<ulong> _rewardedPlayerDbIds = new();
        private readonly HashSet<ulong> _rewardEligiblePlayerDbIds = new();
        private readonly HashSet<ulong> _bossUnlockEligiblePlayerDbIds = new();
        private readonly HashSet<ulong> _progressionEligiblePlayerDbIds = new();
        private readonly HashSet<ulong> _participantsSeenInRunRegion = new();
        private readonly HashSet<ulong> _earlyExitPlayerDbIds = new();
        private readonly HashSet<ulong> _riftEntryBannerSentPlayerDbIds = new();
        private readonly HashSet<ulong> _customPopulationEntityIds = new();
        private readonly HashSet<ulong> _hazardEntityIds = new();
        private readonly HashSet<ulong> _activeBossEntityIds = new();
        private readonly HashSet<ulong> _milestoneMiniBossEntityIds = new();
        private readonly Dictionary<ulong, PlayerState> _readyCheckPlayerStates = new();
        private readonly HashSet<int> _sentTimeWarningThresholds = new();
        private readonly HashSet<int> _sentKillProgressMilestones = new();
        private readonly HashSet<int> _spawnedMilestoneEncounterPercents = new();

        public MythicRiftRunConfig Config { get; private set; }
        public MythicRiftRunStatus Status { get; private set; } = MythicRiftRunStatus.Pending;
        public TimeSpan RegisteredAt { get; private set; }
        public ulong RegionId { get; private set; }
        public ulong BossEntityId => _activeBossEntityIds.FirstOrDefault();
        public ulong ExitPortalEntityId { get; private set; }
        public ulong RewardRoomPortalEntityId { get; private set; }
        public ulong CompletionVendorEntityId { get; private set; }
        public ulong CompletionCrafterEntityId { get; private set; }
        public ulong CompletionEnchanterEntityId { get; private set; }
        public ulong RewardRoomRegionId { get; private set; }
        public bool RewardRoomTeleportOffered { get; private set; }
        public bool RewardRoomTeleportResolved { get; private set; }
        public ulong EffectiveRegionId => RewardRoomRegionId != 0 ? RewardRoomRegionId : RegionId;
        public int CurrentKillCount { get; private set; }
        public bool BossUnlocked { get; private set; }
        public bool RewardsGranted { get; private set; }
        public MythicRiftRewardOutcome RewardOutcome { get; private set; }
        public MythicRiftDifficultySnapshot Difficulty { get; private set; }
        public bool AdmissionTrackingEnabled { get; private set; }
        public bool AdmissionFinalized { get; private set; }
        public TimeSpan StartedAt { get; private set; }
        public TimeSpan? CompletedAt { get; private set; }
        public TimeSpan? ExpiresAt { get; private set; }
        public TimeSpan LastParticipantOnlineAt { get; private set; }
        public TimeSpan NextCustomPopulationSpawnAt { get; private set; }
        public int CustomPopulationTotalSpawned { get; private set; }
        public TimeSpan NextHazardSpawnAt { get; private set; }
        public TimeSpan? ReadyCheckEndsAt { get; private set; }
        public string ReadyCheckLabel { get; private set; }
        public bool RegionDifficultyScalingApplied { get; private set; }
        public int BossSpawnCount { get; private set; }
        public int BossKillCount { get; private set; }
        public int BossGauntletCompletedWaves { get; private set; }
        public float RegionPlayerToMobDamageMultiplierBeforeScaling { get; private set; } = 1f;
        public float RegionMobToPlayerDamageMultiplierBeforeScaling { get; private set; } = 1f;
        public IReadOnlyCollection<ulong> ParticipantPlayerDbIds => _participantPlayerDbIds;
        public IReadOnlyCollection<ulong> AdmittedPlayerDbIds => _admittedPlayerDbIds;
        public IReadOnlyCollection<ulong> RewardedPlayerDbIds => _rewardedPlayerDbIds;
        public IReadOnlyCollection<ulong> RewardEligiblePlayerDbIds => _rewardEligiblePlayerDbIds;
        public IReadOnlyCollection<ulong> BossUnlockEligiblePlayerDbIds => _bossUnlockEligiblePlayerDbIds;
        public IReadOnlyCollection<ulong> ProgressionEligiblePlayerDbIds => _progressionEligiblePlayerDbIds;
        public IReadOnlyCollection<ulong> ParticipantsSeenInRunRegionPlayerDbIds => _participantsSeenInRunRegion;
        public IReadOnlyCollection<ulong> EarlyExitPlayerDbIds => _earlyExitPlayerDbIds;
        public IReadOnlyCollection<ulong> CustomPopulationEntityIds => _customPopulationEntityIds;
        public IReadOnlyCollection<ulong> HazardEntityIds => _hazardEntityIds;
        public IReadOnlyCollection<ulong> ActiveBossEntityIds => _activeBossEntityIds;
        public IReadOnlyDictionary<ulong, PlayerState> ReadyCheckPlayerStates => _readyCheckPlayerStates;
        public int ParticipantCount => _participantPlayerDbIds.Count;
        public int AdmittedPlayerCount => _admittedPlayerDbIds.Count;
        public int EffectivePlayerCount => Difficulty.EffectivePlayerCount;
        public int RewardedPlayerCount => _rewardedPlayerDbIds.Count;
        public bool IsInProgress => Status == MythicRiftRunStatus.Pending || Status == MythicRiftRunStatus.Active;

        public MythicRiftRunState(MythicRiftRunConfig config)
        {
            Config = config;
            Difficulty = config?.Difficulty ?? default;
        }

        public void AttachRegion(ulong regionId)
        {
            RegionId = regionId;
        }

        public void AttachBoss(ulong bossEntityId)
        {
            if (bossEntityId == 0 || _activeBossEntityIds.Add(bossEntityId) == false)
                return;

            BossSpawnCount++;
        }

        public bool IsTrackedBoss(ulong bossEntityId)
        {
            return bossEntityId != 0 && _activeBossEntityIds.Contains(bossEntityId);
        }

        public bool MarkBossDefeated(ulong bossEntityId)
        {
            if (_activeBossEntityIds.Remove(bossEntityId) == false)
                return false;

            BossKillCount++;
            return true;
        }

        public bool RegisterMilestoneMiniBossEntity(ulong entityId)
        {
            return entityId != 0 && _milestoneMiniBossEntityIds.Add(entityId);
        }

        public bool IsMilestoneMiniBossEntity(ulong entityId)
        {
            return entityId != 0 && _milestoneMiniBossEntityIds.Contains(entityId);
        }

        public void MarkBossGauntletWaveCompleted()
        {
            BossGauntletCompletedWaves = Math.Max(BossGauntletCompletedWaves, Config?.WaveNumber ?? 0);
        }

        public void AttachExitPortal(ulong exitPortalEntityId)
        {
            ExitPortalEntityId = exitPortalEntityId;
        }

        public void AttachRewardRoomPortal(ulong rewardRoomPortalEntityId)
        {
            RewardRoomPortalEntityId = rewardRoomPortalEntityId;
        }

        public void AttachCompletionVendor(ulong completionVendorEntityId)
        {
            CompletionVendorEntityId = completionVendorEntityId;
        }

        public void AttachCompletionCrafter(ulong completionCrafterEntityId)
        {
            CompletionCrafterEntityId = completionCrafterEntityId;
        }

        public void AttachCompletionEnchanter(ulong completionEnchanterEntityId)
        {
            CompletionEnchanterEntityId = completionEnchanterEntityId;
        }

        public void MarkRewardRoomTeleportOffered()
        {
            RewardRoomTeleportOffered = true;
        }

        public void AttachRewardRoomRegion(ulong regionId)
        {
            RewardRoomRegionId = regionId;
            RewardRoomTeleportResolved = true;
        }

        public void CaptureRegionDifficultyScaling(float playerToMobDamageMultiplier, float mobToPlayerDamageMultiplier)
        {
            RegionDifficultyScalingApplied = true;
            RegionPlayerToMobDamageMultiplierBeforeScaling = playerToMobDamageMultiplier;
            RegionMobToPlayerDamageMultiplierBeforeScaling = mobToPlayerDamageMultiplier;
        }

        public void ClearRegionDifficultyScaling()
        {
            RegionDifficultyScalingApplied = false;
            RegionPlayerToMobDamageMultiplierBeforeScaling = 1f;
            RegionMobToPlayerDamageMultiplierBeforeScaling = 1f;
        }

        public void SetRegisteredAt(TimeSpan registeredAt)
        {
            RegisteredAt = registeredAt;
            LastParticipantOnlineAt = registeredAt;
        }

        public void Start(TimeSpan startedAt)
        {
            if (Status != MythicRiftRunStatus.Pending)
                return;

            Status = MythicRiftRunStatus.Active;
            StartedAt = startedAt;
            ExpiresAt = startedAt + Config.TimeLimit;
        }

        public void ReplaceConfigForNextBossGauntletWave(MythicRiftRunConfig config)
        {
            if (config == null || Status != MythicRiftRunStatus.Active)
                return;

            Config = config;
            Difficulty = config.Difficulty;
            _activeBossEntityIds.Clear();
            _milestoneMiniBossEntityIds.Clear();
            BossSpawnCount = 0;
            BossKillCount = 0;
            CurrentKillCount = Math.Max(config.KillQuota, 1);
            BossUnlocked = true;
        }

        public void TouchParticipantPresence(TimeSpan currentTime)
        {
            LastParticipantOnlineAt = currentTime;
        }

        public void AddKills(int count)
        {
            if (count <= 0 || Status != MythicRiftRunStatus.Active)
                return;

            CurrentKillCount += count;
            if (CurrentKillCount >= Config.KillQuota)
                BossUnlocked = true;
        }

        public void UnlockBoss()
        {
            if (Status != MythicRiftRunStatus.Active)
                return;

            CurrentKillCount = Math.Max(CurrentKillCount, Config.KillQuota);
            BossUnlocked = true;
        }

        public void ApplyTimePenalty(TimeSpan penalty)
        {
            if (Status != MythicRiftRunStatus.Active || ExpiresAt.HasValue == false || penalty <= TimeSpan.Zero)
                return;

            ExpiresAt = ExpiresAt.Value - penalty;
        }

        public void MarkSuccess(TimeSpan completedAt)
        {
            if (Status != MythicRiftRunStatus.Active)
                return;

            Status = MythicRiftRunStatus.Success;
            CompletedAt = completedAt;
        }

        public void MarkFailed(TimeSpan completedAt)
        {
            if (Status != MythicRiftRunStatus.Active)
                return;

            Status = MythicRiftRunStatus.Failed;
            CompletedAt = completedAt;
        }

        public void MarkAborted(TimeSpan completedAt)
        {
            if (Status == MythicRiftRunStatus.Success || Status == MythicRiftRunStatus.Failed || Status == MythicRiftRunStatus.Aborted)
                return;

            Status = MythicRiftRunStatus.Aborted;
            CompletedAt = completedAt;
        }

        public void MarkRewardsGranted()
        {
            RewardsGranted = true;
        }

        public bool RegisterParticipant(ulong playerDbId)
        {
            if (playerDbId == 0)
                return false;

            if (_earlyExitPlayerDbIds.Contains(playerDbId))
                return false;

            return _participantPlayerDbIds.Add(playerDbId);
        }

        public bool IsParticipant(ulong playerDbId)
        {
            return playerDbId != 0 && _participantPlayerDbIds.Contains(playerDbId);
        }

        public bool MarkParticipantAdmitted(ulong playerDbId)
        {
            if (AdmissionTrackingEnabled == false || AdmissionFinalized || IsParticipant(playerDbId) == false || _earlyExitPlayerDbIds.Contains(playerDbId))
                return false;

            return _admittedPlayerDbIds.Add(playerDbId);
        }

        public bool RemoveParticipantBeforeAdmission(ulong playerDbId)
        {
            if (AdmissionTrackingEnabled == false || AdmissionFinalized || playerDbId == 0 || _admittedPlayerDbIds.Contains(playerDbId))
                return false;

            return _participantPlayerDbIds.Remove(playerDbId);
        }

        public void EnableAdmissionTracking()
        {
            if (AdmissionFinalized == false)
                AdmissionTrackingEnabled = true;
        }

        public void FinalizeAdmission()
        {
            if (AdmissionTrackingEnabled == false || AdmissionFinalized)
                return;

            int admittedPlayerCount = Math.Max(_admittedPlayerDbIds.Count, 1);
            Difficulty = MythicRiftScaling.BuildSnapshot(Config.RiftLevel, admittedPlayerCount, Config.Mode);
            AdmissionFinalized = true;
        }

        public bool MarkParticipantLeftEarly(ulong playerDbId)
        {
            if (playerDbId == 0)
                return false;

            bool wasParticipant = _participantPlayerDbIds.Remove(playerDbId);
            _admittedPlayerDbIds.Remove(playerDbId);
            _participantsSeenInRunRegion.Remove(playerDbId);
            _bossUnlockEligiblePlayerDbIds.Remove(playerDbId);
            _progressionEligiblePlayerDbIds.Remove(playerDbId);
            _rewardEligiblePlayerDbIds.Remove(playerDbId);
            _riftEntryBannerSentPlayerDbIds.Remove(playerDbId);
            _earlyExitPlayerDbIds.Add(playerDbId);
            return wasParticipant;
        }

        public bool HasParticipantLeftEarly(ulong playerDbId)
        {
            return playerDbId != 0 && _earlyExitPlayerDbIds.Contains(playerDbId);
        }

        public bool MarkParticipantSeenInRunRegion(ulong playerDbId)
        {
            if (playerDbId == 0)
                return false;

            if (_earlyExitPlayerDbIds.Contains(playerDbId))
                return false;

            return _participantsSeenInRunRegion.Add(playerDbId);
        }

        public bool HasParticipantBeenSeenInRunRegion(ulong playerDbId)
        {
            return playerDbId != 0 && _participantsSeenInRunRegion.Contains(playerDbId);
        }

        public bool MarkRiftEntryBannerSent(ulong playerDbId)
        {
            if (playerDbId == 0)
                return false;

            return _riftEntryBannerSentPlayerDbIds.Add(playerDbId);
        }

        public bool HasRewardForPlayer(ulong playerDbId)
        {
            return playerDbId != 0 && _rewardedPlayerDbIds.Contains(playerDbId);
        }

        public bool MarkRewardGrantedToPlayer(ulong playerDbId)
        {
            if (playerDbId == 0)
                return false;

            bool added = _rewardedPlayerDbIds.Add(playerDbId);
            if (_rewardedPlayerDbIds.Count > 0)
                RewardsGranted = true;

            return added;
        }

        public void SetRewardOutcome(MythicRiftRewardOutcome rewardOutcome)
        {
            RewardOutcome = rewardOutcome;
        }

        public void SnapshotBossUnlockEligiblePlayers(IEnumerable<ulong> playerDbIds)
        {
            _bossUnlockEligiblePlayerDbIds.Clear();
            if (playerDbIds == null)
                return;

            foreach (ulong playerDbId in playerDbIds)
            {
                if (playerDbId != 0)
                    _bossUnlockEligiblePlayerDbIds.Add(playerDbId);
            }
        }

        public void SnapshotProgressionEligiblePlayers(IEnumerable<ulong> playerDbIds)
        {
            _progressionEligiblePlayerDbIds.Clear();
            if (playerDbIds == null)
                return;

            foreach (ulong playerDbId in playerDbIds)
            {
                if (playerDbId != 0 && _bossUnlockEligiblePlayerDbIds.Contains(playerDbId))
                    _progressionEligiblePlayerDbIds.Add(playerDbId);
            }
        }

        public void SnapshotRewardEligiblePlayers(IEnumerable<ulong> playerDbIds)
        {
            _rewardEligiblePlayerDbIds.Clear();
            if (playerDbIds == null)
                return;

            foreach (ulong playerDbId in playerDbIds)
            {
                if (IsParticipant(playerDbId) && HasParticipantLeftEarly(playerDbId) == false)
                    _rewardEligiblePlayerDbIds.Add(playerDbId);
            }
        }

        public bool IsRewardEligible(ulong playerDbId)
        {
            return playerDbId != 0 && _rewardEligiblePlayerDbIds.Contains(playerDbId);
        }

        public bool MarkTimeWarningSent(int thresholdSeconds)
        {
            return thresholdSeconds > 0 && _sentTimeWarningThresholds.Add(thresholdSeconds);
        }

        public bool MarkKillProgressMilestoneSent(int milestonePercent)
        {
            return milestonePercent > 0 && _sentKillProgressMilestones.Add(milestonePercent);
        }

        public bool HasSpawnedMilestoneEncounter(int milestonePercent)
        {
            return milestonePercent > 0 && _spawnedMilestoneEncounterPercents.Contains(milestonePercent);
        }

        public bool MarkMilestoneEncounterSpawned(int milestonePercent)
        {
            return milestonePercent > 0 && _spawnedMilestoneEncounterPercents.Add(milestonePercent);
        }

        public void SetNextCustomPopulationSpawnAt(TimeSpan nextSpawnAt)
        {
            NextCustomPopulationSpawnAt = nextSpawnAt;
        }

        public bool RegisterCustomPopulationEntity(ulong entityId)
        {
            if (entityId == 0)
                return false;

            bool added = _customPopulationEntityIds.Add(entityId);
            if (added)
                CustomPopulationTotalSpawned++;

            return added;
        }

        public bool RemoveCustomPopulationEntity(ulong entityId)
        {
            return entityId != 0 && _customPopulationEntityIds.Remove(entityId);
        }

        public bool RegisterHazardEntity(ulong entityId)
        {
            return entityId != 0 && _hazardEntityIds.Add(entityId);
        }

        public bool RemoveHazardEntity(ulong entityId)
        {
            return entityId != 0 && _hazardEntityIds.Remove(entityId);
        }

        public void SetNextHazardSpawnAt(TimeSpan nextSpawnAt)
        {
            NextHazardSpawnAt = nextSpawnAt;
        }

        public void BeginReadyCheck(TimeSpan endsAt, string label, IEnumerable<ulong> playerDbIds)
        {
            ReadyCheckEndsAt = endsAt;
            ReadyCheckLabel = string.IsNullOrWhiteSpace(label) ? "Rift wave" : label;
            _readyCheckPlayerStates.Clear();

            if (playerDbIds == null)
                return;

            foreach (ulong playerDbId in playerDbIds)
            {
                if (playerDbId != 0 && IsParticipant(playerDbId) && HasParticipantLeftEarly(playerDbId) == false)
                    _readyCheckPlayerStates[playerDbId] = PlayerState.Pending;
            }
        }

        public bool IsReadyCheckActive(TimeSpan currentTime)
        {
            return ReadyCheckEndsAt.HasValue && currentTime < ReadyCheckEndsAt.Value;
        }

        public void ClearReadyCheck()
        {
            ReadyCheckEndsAt = null;
            ReadyCheckLabel = null;
            _readyCheckPlayerStates.Clear();
        }

        public PlayerState GetReadyCheckPlayerState(ulong playerDbId)
        {
            return _readyCheckPlayerStates.TryGetValue(playerDbId, out PlayerState state)
                ? state
                : PlayerState.Fallback;
        }

        public bool SetReadyCheckPlayerState(ulong playerDbId, PlayerState state)
        {
            if (playerDbId == 0 || _readyCheckPlayerStates.ContainsKey(playerDbId) == false)
                return false;

            _readyCheckPlayerStates[playerDbId] = state;
            return true;
        }

        public bool AreReadyCheckPlayersReady(IEnumerable<ulong> activePlayerDbIds)
        {
            if (ReadyCheckEndsAt.HasValue == false)
                return true;

            bool hasTrackedActivePlayer = false;
            foreach (ulong playerDbId in activePlayerDbIds ?? Array.Empty<ulong>())
            {
                if (playerDbId == 0 || IsParticipant(playerDbId) == false || HasParticipantLeftEarly(playerDbId))
                    continue;

                hasTrackedActivePlayer = true;
                if (GetReadyCheckPlayerState(playerDbId) == PlayerState.Pending)
                    return false;
            }

            return hasTrackedActivePlayer && _readyCheckPlayerStates.Count > 0;
        }

        public bool HasExpired(TimeSpan currentTime)
        {
            if (Config?.UseBossGauntletMode == true)
                return false;

            return Status == MythicRiftRunStatus.Active
                && ExpiresAt.HasValue
                && currentTime >= ExpiresAt.Value;
        }

        public TimeSpan GetTimeRemaining(TimeSpan currentTime)
        {
            if (ExpiresAt.HasValue == false)
                return Config.TimeLimit;

            TimeSpan remaining = ExpiresAt.Value - currentTime;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
