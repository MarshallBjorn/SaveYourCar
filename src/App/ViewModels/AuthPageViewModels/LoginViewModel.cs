using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using App.Support;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Entities;
using Core.DTOs;

namespace App.ViewModels.AuthPageViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public Action? OnLoggingInSuccess { get; set; }
    public Action? SwitchToRegister { get; set; }

    [ObservableProperty]
    public string _email = "";
    [ObservableProperty]
    private string _password = "";
    [ObservableProperty]
    private string _messageText = "";

    [RelayCommand]
    private async Task LogIn()
    {
        MessageText = "";
        var client = HttpClientFactory.CreateClient("CarInsuranceApi");

        try {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {   
                MessageText = $"Email or password fields should not be empty.";
                return;
            }

            var response = await client.PostAsJsonAsync("api/User/login", new {
                Email,
                Password
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                MessageText = $"Wrong email or password.";
                return;
            }
            
            var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

            if (result is null || string.IsNullOrEmpty(result.Token))
            {
                MessageText = "Login failed: Invalid response from server.";
                return;
            }

            TokenStorage.Token = result.Token;
            await LoginSuccess();
        } 
        catch (Exception ex)
        {
            MessageText = ex.Message;
        }   
    }

    private async Task LoginSuccess()
    {
        var client = HttpClientFactory.CreateClient("CarInsuranceApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenStorage.Token);
        
        try {
            var response = await client.GetAsync("api/User/me");

            if(!response.IsSuccessStatusCode)
            {
                MessageText = $"Failed to load user info: {response.StatusCode}";
                return;
            }

            var user = await response.Content.ReadFromJsonAsync<User>();

            if (user != null)
            {
                AppState.LoggedInUser = user;
                AppState.RaiseLogin();
                OnLoggingInSuccess?.Invoke();
                MessageText = $"Welcome {user.FirstName} {user.LastName}";
            }
        }
        catch (Exception ex)
        {
            MessageText = ex.Message;
        }
    }

    [RelayCommand]
    public void SwitchToReg()
    {
        SwitchToRegister?.Invoke();
    }

    private void reset()
    {
        Email = "";
        Password = "";
    }
}