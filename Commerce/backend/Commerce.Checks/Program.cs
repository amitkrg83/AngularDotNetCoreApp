using System.Net;
using System.Security.Cryptography;
using System.Text;
using Commerce.Application;
using Commerce.Domain;
using Commerce.Infrastructure;

int checks=0;
void Check(bool valid,string name){if(!valid)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
void Reject(Action action,string name){try{action();}catch(ShopException){Check(true,name);return;}throw new Exception("Accepted "+name);}
var secret="whsec_unit_test_secret";
using var http=new HttpClient();var stripe=new StripeGateway(http,"sk_test_unused",secret,"http://localhost:4200");
string Body(string type="checkout.session.completed",string status="paid",bool live=false)=>System.Text.Json.JsonSerializer.Serialize(new { livemode=live, type, data=new { @object=new { id="cs_test_example", payment_status=status, amount_total=2500, currency="usd" } } });
string Sign(string body,long? timestamp=null){var t=timestamp??DateTimeOffset.UtcNow.ToUnixTimeSeconds();var hash=HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes($"{t}.{body}"));return $"t={t},v1={Convert.ToHexString(hash).ToLowerInvariant()}";}
var body=Body();var evt=stripe.VerifyEvent(body,Sign(body));Check(evt?.Status=="Paid"&&evt.AmountCents==2500,"verified paid callback");
Reject(()=>stripe.VerifyEvent(body,Sign(body+"tampered")),"tampered webhook rejected");
Reject(()=>stripe.VerifyEvent(body,Sign(body,DateTimeOffset.UtcNow.ToUnixTimeSeconds()-600)),"stale webhook rejected");
var live=Body(live:true);Reject(()=>stripe.VerifyEvent(live,Sign(live)),"live events rejected");
var unpaid=Body(status:"unpaid");Check(stripe.VerifyEvent(unpaid,Sign(unpaid)) is null,"unpaid completion never marks paid");
var expired=Body(type:"checkout.session.expired");Check(stripe.VerifyEvent(expired,Sign(expired))?.Status=="Expired","expiration mapped");
var passwords=new PasswordService();var hash=passwords.Hash("PasswordForTests123!");Check(passwords.Verify("PasswordForTests123!",hash),"password verification");Check(!passwords.Verify("wrong",hash),"incorrect password rejected");Check(passwords.Hash("PasswordForTests123!")!=hash,"password salts unique");
// Test the actual Stripe request shape without contacting Stripe.
var handler=new CaptureHandler();using var fake=new HttpClient(handler);var gateway=new StripeGateway(fake,"sk_test_unused",secret,"http://localhost:4200");
var order=new Order(Guid.NewGuid(),Guid.NewGuid(),[new(Guid.NewGuid(),"Keyboard",12.50m,2)],new("Name","Street","City","ZIP","US"),25m,"Pending","Unfulfilled",DateTimeOffset.UtcNow,"key","fingerprint");
var session=await gateway.CreateAsync(order,"buyer@example.test",CancellationToken.None);
Check(session.Id=="cs_test_example","checkout session parsed");
Check(handler.Form.Contains("unit_amount%5D=1250")&&handler.Form.Contains("quantity%5D=2"),"server prices converted to cents");
Check(handler.Idempotency==order.Id.ToString(),"provider idempotency key is order ID");
Console.WriteLine($"{checks} checks passed.");
sealed class CaptureHandler : HttpMessageHandler
{
    public string Form="";public string Idempotency="";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        Form=await request.Content!.ReadAsStringAsync(ct);Idempotency=request.Headers.GetValues("Idempotency-Key").Single();
        return new(HttpStatusCode.OK){Content=new StringContent("{\"id\":\"cs_test_example\",\"url\":\"https://checkout.stripe.com/c/pay/test\"}")};
    }
}
