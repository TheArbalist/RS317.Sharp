﻿using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using Common.Logging;
using Glader.Essentials;
using GladMMO;
using Rs317.GladMMMO;
using Rs317.Sharp;

namespace Rs317.GladMMO
{
	public sealed class InstanceServerDependencyModule : Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.RegisterModule<GameServerNetworkClientAutofacModule>();
			builder.RegisterModule(new GameClientMessageHandlerAutofacModule(GameSceneType.InstanceServerScene, this.GetType().Assembly));
			builder.RegisterModule<GladMMONetworkSerializerAutofacModule>();
			builder.RegisterModule<RsGameplayDependencyRegisterationAutofacModule>();
			builder.RegisterModule<CharacterServiceDependencyAutofacModule>();
			builder.RegisterModule<GladMMOClientExplicitEngineInterfaceAutoModule>();

			builder.RegisterInstance<GameManager>(GladMMOProgram.RootGameManager)
				.As<IGameContextEventQueueable>()
				.As<IGameServiceable>()
				.AsSelf()
				.ExternallyOwned()
				.SingleInstance();

			builder.Register<GladMMOUnityClient>(context =>
				{
					//This is done to make sure only 1 is ever created.
					if(GladMMOProgram.RootClient == null)
					{
						GladMMOProgram.RootClient = new GladMMOUnityClient(context.Resolve<ClientConfiguration>(), context.Resolve<UnityRsGraphics>(), GladMMOProgram.RootGameManager);

						return GladMMOProgram.RootClient;
					}
					else
						return GladMMOProgram.RootClient;
				})
				.AsSelf()
				.As<RsUnityClient>()
				.AsImplementedInterfaces()
				.ExternallyOwned();

			//Register all required Authentication/Title modules.
			builder.RegisterModule(new CommonGameDependencyModule(GameSceneType.InstanceServerScene, "http://192.168.0.12:5000", typeof(GladMMOUnityClient).Assembly));

			builder.RegisterInstance(new ConsoleLogger(LogLevel.All))
				.AsImplementedInterfaces()
				.SingleInstance();

			//LocalZoneDataRepository : IZoneDataRepository
			builder.RegisterType<LocalZoneDataRepository>()
				.As<IZoneDataRepository>()
				.As<IReadonlyZoneDataRepository>();
		}
	}
}
