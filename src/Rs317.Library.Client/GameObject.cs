namespace Rs317.Sharp
{
	public sealed class GameObject : Animable
	{
		private int frame;

		private int[] childrenIds;

		private int varBitId;

		private int configId;
		private int vertexHeightBottomLeft;
		private int vertexHeightBottomRight;
		private int vertexHeightTopRight;
		private int vertexHeightTopLeft;
		private AnimationSequence animation;
		private int nextFrameTime;
		public static IBaseClient clientInstance;
		private int objectId;
		private int type;
		private int orientation;

		public GameObject(int objectId, int orientation, int type, int vertexHeightBottomRight, int vertexHeightTopRight,
			int vertexHeightBottomLeft, int vertexHeightTopLeft, int animationId, bool animating)
		{
			this.objectId = objectId;
			this.type = type;
			this.orientation = orientation;
			this.vertexHeightBottomLeft = vertexHeightBottomLeft;
			this.vertexHeightBottomRight = vertexHeightBottomRight;
			this.vertexHeightTopRight = vertexHeightTopRight;
			this.vertexHeightTopLeft = vertexHeightTopLeft;
			if(animationId != -1)
			{
				animation = AnimationSequence.animations[animationId];
				frame = 0;
				nextFrameTime = clientInstance.CurrentTick;
				if(animating && animation.frameStep != -1)
				{
					frame = (int)(StaticRandomGenerator.Next() * animation.frameCount);
					nextFrameTime -= (int)(StaticRandomGenerator.Next() * animation.getFrameLength(frame));
				}
			}

			GameObjectDefinition definition = GameObjectDefinition.getDefinition(this.objectId);
			varBitId = definition.varBitId;
			configId = definition.configIds;
			childrenIds = definition.childIds;
		}

		private GameObjectDefinition getChildDefinition()
		{
			int child = -1;
			if(varBitId != -1)
			{
				VarBit varBit = VarBit.values[varBitId];
				int configId = varBit.configId;
				int lsb = varBit.leastSignificantBit;
				int msb = varBit.mostSignificantBit;
				int bit = ConstantData.GetBitfieldMaxValue(msb - lsb);
				child = clientInstance.GetInterfaceSettings(configId) >> lsb & bit;
			}
			else if(configId != -1)
				child = clientInstance.GetInterfaceSettings(configId);

			if(child < 0 || child >= childrenIds.Length || childrenIds[child] == -1)
				return null;
			else
				return GameObjectDefinition.getDefinition(childrenIds[child]);
		}

		public override Model getRotatedModel()
		{
			int animationId = -1;
			if(animation != null)
			{
				int step = clientInstance.CurrentTick - nextFrameTime;
				if(step > 100 && animation.frameStep > 0)
					step = 100;
				while(step > animation.getFrameLength(frame))
				{
					step -= animation.getFrameLength(frame);
					frame++;
					if(frame < animation.frameCount)
						continue;
					frame -= animation.frameStep;
					if(frame >= 0 && frame < animation.frameCount)
						continue;
					animation = null;
					break;
				}

				nextFrameTime = clientInstance.CurrentTick - step;
				if(animation != null)
					animationId = animation.primaryFrames[frame];
			}

			GameObjectDefinition definition;
			if(childrenIds != null)
				definition = getChildDefinition();
			else
				definition = GameObjectDefinition.getDefinition(objectId);
			if(definition == null)
			{
				return null;
			}
			else
			{
				return definition.getModelAt(type, orientation, vertexHeightBottomLeft, vertexHeightBottomRight,
					vertexHeightTopRight, vertexHeightTopLeft, animationId);
			}
		}
	}
}
