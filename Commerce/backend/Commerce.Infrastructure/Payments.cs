using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Commerce.Application;
using Commerce.Domain;
namespace Commerce.Infrastructure;

public sealed class SimulatedGateway(string origin) : IPaymentGateway
{
    public string Mode=>"Simulated";
    public Task<PaymentSession> CreateAsync(Order order,string email,CancellationToken ct)=>
        Task.FromResult(new PaymentSession($"sim_{order.Id}",$"{origin}/payment/{order.Id}"));
    public GatewayEvent? VerifyEvent(string body,string signature)=>throw new ShopException(404,"Stripe is not enabled.");
}
// Provider adapter uses Stripe REST directly, keeping SDK types out of Application.
public sealed class StripeGateway(HttpClient http,string key,string webhookSecret,string origin) : IPaymentGateway
{
    public string Mode=>"Stripe";
    public async Task<PaymentSession> CreateAsync(Order order,string email,CancellationToken ct)
    {
        var fields=new Dictionary<string,string>{["mode"]="payment",["payment_method_types[0]"]="card",["customer_email"]=email,
            ["client_reference_id"]=order.Id.ToString(),["success_url"]=$"{origin}/orders?order={order.Id}",
            ["cancel_url"]=$"{origin}/orders?order={order.Id}&canceled=1",["metadata[order_id]"]=order.Id.ToString()};
        for(var i=0;i<order.Lines.Length;i++)
        {
            var line=order.Lines[i];var prefix=$"line_items[{i}]";
            fields[$"{prefix}[quantity]"]=line.Quantity.ToString(CultureInfo.InvariantCulture);
            fields[$"{prefix}[price_data][currency]"]="usd";
            fields[$"{prefix}[price_data][unit_amount]"]=((long)(line.UnitPrice*100)).ToString(CultureInfo.InvariantCulture);
            fields[$"{prefix}[price_data][product_data][name]"]=line.Name;
        }
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.stripe.com/v1/checkout/sessions");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        request.Headers.Add("Stripe-Version","2025-06-30.basil");
        request.Headers.Add("Idempotency-Key",order.Id.ToString());
        request.Content=new FormUrlEncodedContent(fields);
        using var response=await http.SendAsync(request,ct);
        if(!response.IsSuccessStatusCode) throw new ShopException(502,"Payment provider rejected checkout. Check server-side Stripe test configuration, then retry the same checkout.");
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return new(doc.RootElement.GetProperty("id").GetString()!,doc.RootElement.GetProperty("url").GetString()!);
    }
    public GatewayEvent? VerifyEvent(string body,string signature)
    {
        var values=signature.Split(',').Select(x=>x.Split('=',2)).Where(x=>x.Length==2).ToArray();
        var timestamp=values.FirstOrDefault(x=>x[0]=="t")?[1];
        if(!long.TryParse(timestamp,out var seconds) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds()-seconds)>300)
            throw new ShopException(400,"Invalid webhook timestamp.");
        var expected=HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret),Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        bool matches=false;
        foreach(var value in values.Where(x=>x[0]=="v1"))
        {
            try { matches |= CryptographicOperations.FixedTimeEquals(expected,Convert.FromHexString(value[1])); }
            catch(FormatException) { }
        }
        if(!matches) throw new ShopException(400,"Invalid webhook signature.");
        using var doc=JsonDocument.Parse(body);var root=doc.RootElement;
        if(root.GetProperty("livemode").GetBoolean()) throw new ShopException(400,"Live payments are disabled in this demo.");
        var type=root.GetProperty("type").GetString();var session=root.GetProperty("data").GetProperty("object");
        var state=type switch {
            "checkout.session.completed" when session.GetProperty("payment_status").GetString()=="paid"=>"Paid",
            "checkout.session.async_payment_succeeded"=>"Paid",
            "checkout.session.async_payment_failed"=>"Failed",
            "checkout.session.expired"=>"Expired",
            _=>null
        };
        return state is null ? null : new(session.GetProperty("id").GetString()!,state,session.GetProperty("amount_total").GetInt64(),session.GetProperty("currency").GetString()!);
    }
}
