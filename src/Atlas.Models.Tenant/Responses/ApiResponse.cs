using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Models.Tenant.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public ApiResponse() { }

        public ApiResponse(T data, string? message = null)
        {
            Success = true;
            Data = data;
            Message = message;
        }

        public ApiResponse(string message, bool success = false)
        {
            Success = success;
            Message = message;
        }

        public static ApiResponse<T> Ok(T data, string? message = null)
            => new(data, message);

        public static ApiResponse<T> Fail(string message)
            => new(message, false);
    }
}
