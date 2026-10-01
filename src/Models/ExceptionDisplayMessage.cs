using System;

namespace SPConverter.Models;

public static class ExceptionDisplayMessage
{
    public static string From(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}
