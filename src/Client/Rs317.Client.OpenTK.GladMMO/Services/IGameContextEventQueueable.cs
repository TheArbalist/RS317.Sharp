﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Rs317.GladMMO
{
	public interface IGameContextEventQueueable
	{
		void Enqueue(Action actionEvent);
	}
}
