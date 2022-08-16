// Copyright (c) 2020 Bitcoin Association

using System;

namespace Common.BitcoinRpc
{
	public class RpcException : Exception
	{
		public RpcException(string message, Exception innerException) : base(message, innerException)
		{
			Code = 0;
		}

		public RpcException(int code, string message) : base(message)
		{
			Code = code;
		}

		public int Code { get; private set; }
	}
}
