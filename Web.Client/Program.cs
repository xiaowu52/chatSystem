var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// ���HttpClient����
builder.Services.AddHttpClient();

// ��ӻỰ����
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ��� CORS ����
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.WithOrigins("https://localhost:5284")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// �� Program.cs �����
app.MapGet("/", context => {
    context.Response.Redirect("/Home/Login");
    return Task.CompletedTask;
});


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("SignalRPolicy");
app.UseAuthorization();

// ���ûỰ
app.UseSession();

app.MapRazorPages();
app.MapControllers();

app.Run();
