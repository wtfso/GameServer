﻿using ENet;
using LeagueSandbox.GameServer.Logic.Packets;
using LeagueSandbox.GameServer.Logic.Chatbox;
using static LeagueSandbox.GameServer.Logic.Chatbox.ChatCommandManager;
using LeagueSandbox.GameServer.Logic.Players;
using System;

namespace LeagueSandbox.GameServer.Core.Logic.PacketHandlers.Packets
{
    class HandleChatBoxMessage : IPacketHandler
    {
        private Game _game = Program.ResolveDependency<Game>();
        private ChatCommandManager _chatCommandManager = Program.ResolveDependency<ChatCommandManager>();
        private PlayerManager _playerManager = Program.ResolveDependency<PlayerManager>();
        private Logger _logger = Program.ResolveDependency<Logger>();

        public bool HandlePacket(Peer peer, byte[] data)
        {
            var message = new ChatMessage(data);
            Console.WriteLine("PlayerID: " + message.playerId);
            var split = message.msg.Split(' ');
            if (split.Length > 1)
            {
                int x, y = 0;
                if (int.TryParse(split[0], out x))
                {
                    if (int.TryParse(split[1], out y))
                    {
                        var response = new AttentionPingAns(
                            _playerManager.GetPeerInfo(peer),
                            new AttentionPing { x = x, y = y, targetNetId = 0, type = 0 }
                        );
                        _game.PacketHandlerManager.broadcastPacketTeam(
                            _playerManager.GetPeerInfo(peer).Team, response, Channel.CHL_S2C
                        );
                    }
                }
            }

            #region Commands
            // Execute commands
            var CommandStarterCharacter = _chatCommandManager.CommandStarterCharacter;
            if (message.msg.StartsWith(CommandStarterCharacter))
            {
                message.msg = message.msg.Remove(0, 1);
                split = message.msg.ToLower().Split(' ');

                ChatCommand command = _chatCommandManager.GetCommand(split[0]);
                if (command != null)
                {
                    try
                    {
                        command.Execute(peer, true, message.msg);
                    }
                    catch
                    {
                        _logger.LogCoreWarning(command + " sent an exception.");
                        var dm = new DebugMessage("Something went wrong...Did you wrote the command well ? ");
                        _game.PacketHandlerManager.sendPacket(peer, dm, Channel.CHL_S2C);
                    }
                    return true;
                }
                else
                {
                    _chatCommandManager.SendDebugMsgFormatted(DebugMsgType.ERROR, "<font color =\"#E175FF\"><b>" + _chatCommandManager.CommandStarterCharacter + split[0] + "</b><font color =\"#AFBF00\"> is not a valid command.");
                    _chatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, "Type <font color =\"#E175FF\"><b>" + _chatCommandManager.CommandStarterCharacter + "help</b><font color =\"#AFBF00\"> for a list of available commands");
                    return true;
                }
            }
            #endregion

            var debugMessage = string.Format("{0} ({1}): </font><font color=\"#FFFFFF\">{2}",
                _playerManager.GetPeerInfo(peer).Name,
                _playerManager.GetPeerInfo(peer).Champion.Model,
                message.msg);
            var teamChatColor = "<font color=\"#00FF00\">";
            var enemyChatColor = "<font color=\"#FF0000\">";
            var dmTeam = new DebugMessage(teamChatColor + "[All] " + debugMessage);
            var dmEnemy = new DebugMessage(enemyChatColor + "[All] " + debugMessage);
            var ownTeam = _playerManager.GetPeerInfo(peer).Team;
            var enemyTeam = CustomConvert.GetEnemyTeam(ownTeam);

            if (_game.Config.ChatCheatsEnabled)
            {
                _game.PacketHandlerManager.broadcastPacketTeam(ownTeam, dmTeam, Channel.CHL_S2C);
                _game.PacketHandlerManager.broadcastPacketTeam(enemyTeam, dmEnemy, Channel.CHL_S2C);
                return true;
            }

            switch (message.type)
            {
                case ChatType.CHAT_ALL:
                    _game.PacketHandlerManager.broadcastPacketTeam(ownTeam, dmTeam, Channel.CHL_S2C);
                    _game.PacketHandlerManager.broadcastPacketTeam(enemyTeam, dmEnemy, Channel.CHL_S2C);
                    return true;
                case ChatType.CHAT_TEAM:
                    dmTeam = new DebugMessage(teamChatColor + debugMessage);
                    _game.PacketHandlerManager.broadcastPacketTeam(ownTeam, dmTeam, Channel.CHL_S2C);
                    return true;
                default:
                    //Logging.errorLine("Unknown ChatMessageType");
                    return _game.PacketHandlerManager.sendPacket(peer, data, Channel.CHL_COMMUNICATION);
            }
        }
    }
}
