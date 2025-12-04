using System;
using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// NPC behavior for quest givers using ScriptBase.
/// Demonstrates NPC interaction, dialogue management, and quest offering.
/// Shows visual quest indicators (! exclamation mark above NPC).
/// </summary>
public class QuestGiverBehavior : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize quest giver state on first tick
        if (!Context.HasState<QuestGiverState>())
        {
            Context.World.Add(
                Context.Entity.Value,
                new QuestGiverState
                {
                    QuestId = "catch_5_pokemon", // Default quest - should be set per NPC
                    HasOfferedQuest = false,
                    QuestCompleted = false,
                    ShowIndicator = true,
                    DialogueState = DialogueState.Idle,
                }
            );
        }

        Context.Logger.LogInformation(
            "Quest giver NPC initialized at position ({X}, {Y})",
            Context.Position.X,
            Context.Position.Y
        );
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Listen for player interaction
        On<TickEvent>(evt =>
        {
            ref var state = ref Context.GetState<QuestGiverState>();
            ref var position = ref Context.Position;

            // Update quest indicator (! mark) visibility
            UpdateQuestIndicator(ref state);

            // Check for player interaction
            CheckPlayerInteraction(ref state);
        });

        // Listen for quest acceptance
        On<QuestAcceptedEvent>(evt =>
        {
            ref var state = ref Context.GetState<QuestGiverState>();

            // Check if this is our quest
            if (evt.QuestId == state.QuestId)
            {
                state.HasOfferedQuest = true;
                state.ShowIndicator = false;
                state.DialogueState = DialogueState.QuestActive;

                Context.Logger.LogInformation(
                    "Player accepted quest from NPC: {QuestId}",
                    state.QuestId
                );
            }
        });

        // Listen for quest completion
        On<QuestCompletedEvent>(evt =>
        {
            ref var state = ref Context.GetState<QuestGiverState>();

            // Check if this is our quest
            if (evt.QuestId == state.QuestId)
            {
                state.QuestCompleted = true;
                state.ShowIndicator = true; // Show gold ! for turn-in
                state.DialogueState = DialogueState.QuestComplete;

                Context.Logger.LogInformation(
                    "Quest ready for turn-in at NPC: {QuestId}",
                    state.QuestId
                );
            }
        });
    }

    private void UpdateQuestIndicator(ref QuestGiverState state)
    {
        // In a real implementation, this would update sprite/UI
        // For now, just log state changes

        if (state.ShowIndicator && state.DialogueState == DialogueState.Idle)
        {
            // Show gray ! (quest available)
            Context.Logger.LogTrace("Quest indicator: Available (!)");
        }
        else if (state.ShowIndicator && state.DialogueState == DialogueState.QuestComplete)
        {
            // Show gold ! (quest complete, ready for turn-in)
            Context.Logger.LogTrace("Quest indicator: Complete (!)");
        }
        else if (state.DialogueState == DialogueState.QuestActive)
        {
            // No indicator (quest in progress)
            Context.Logger.LogTrace("Quest indicator: In Progress (no marker)");
        }
    }

    private void CheckPlayerInteraction(ref QuestGiverState state)
    {
        // Check if player is adjacent and pressed interaction button
        // This is simplified - real implementation would check input events

        var playerEntity = Context.Player.GetPlayerEntity();
        if (!playerEntity.HasValue)
            return;

        ref var playerPos = ref Context.World.Get<MonoBallFramework.Game.Components.Movement.Position>(
            playerEntity.Value
        );
        ref var npcPos = ref Context.Position;

        // Check if player is adjacent (within 1 tile)
        int distance = Math.Abs(playerPos.X - npcPos.X) + Math.Abs(playerPos.Y - npcPos.Y);

        if (distance <= 1)
        {
            // Player is adjacent - handle dialogue based on quest state
            HandleDialogue(ref state, playerEntity.Value);
        }
    }

    private void HandleDialogue(ref QuestGiverState state, Entity player)
    {
        switch (state.DialogueState)
        {
            case DialogueState.Idle:
                // Offer quest
                OfferQuest(ref state, player);
                break;

            case DialogueState.QuestActive:
                // Check progress
                ShowQuestProgress(ref state);
                break;

            case DialogueState.QuestComplete:
                // Give rewards
                CompleteQuest(ref state, player);
                break;

            case DialogueState.Finished:
                // Thank you dialogue
                ShowThankYou();
                break;
        }
    }

    private void OfferQuest(ref QuestGiverState state, Entity player)
    {
        Context.Logger.LogInformation("NPC offering quest: {QuestId}", state.QuestId);

        // Publish quest offered event
        Publish(
            new QuestOfferedEvent
            {
                Entity = Context.Entity.Value,
                QuestId = state.QuestId,
                PlayerId = player,
            }
        );

        // Show dialogue: "Would you like to help me catch 5 Pokémon?"
        // In real implementation, this would trigger dialogue UI

        // Simulate player accepting (in real game, wait for player choice)
        AcceptQuest(ref state, player);
    }

    private void AcceptQuest(ref QuestGiverState state, Entity player)
    {
        // Publish quest accepted event
        Publish(new QuestAcceptedEvent { Entity = player, QuestId = state.QuestId });
    }

    private void ShowQuestProgress(ref QuestGiverState state)
    {
        Context.Logger.LogInformation("NPC showing quest progress for: {QuestId}", state.QuestId);

        // Show dialogue: "Keep working on it! You're doing great!"
        // In real implementation, check actual progress and show specific feedback
    }

    private void CompleteQuest(ref QuestGiverState state, Entity player)
    {
        Context.Logger.LogInformation("NPC completing quest: {QuestId}", state.QuestId);

        // Show dialogue: "Thank you so much! Here's your reward!"
        // Rewards are handled by quest_reward_handler.csx

        state.DialogueState = DialogueState.Finished;
        state.ShowIndicator = false;
    }

    private void ShowThankYou()
    {
        Context.Logger.LogInformation("NPC showing thank you dialogue");

        // Show dialogue: "Thanks again for your help!"
    }

    public override void OnUnload()
    {
        Context.Logger.LogInformation("Quest giver NPC deactivated");

        if (Context.HasState<QuestGiverState>())
        {
            Context.RemoveState<QuestGiverState>();
        }

        base.OnUnload();
    }
}

// Component to store quest giver state
public struct QuestGiverState
{
    public string QuestId;
    public bool HasOfferedQuest;
    public bool QuestCompleted;
    public bool ShowIndicator;
    public DialogueState DialogueState;
}

public enum DialogueState
{
    Idle, // Not interacted yet
    QuestActive, // Quest given, not completed
    QuestComplete, // Quest done, ready for turn-in
    Finished, // Reward given, done
}

return new QuestGiverBehavior();
