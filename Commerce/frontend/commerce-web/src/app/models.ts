export interface Product { id:string; name:string; description:string; price:number; category:string; stockQuantity:number; imageUrl:string; active:boolean; }
export type ProductInput=Omit<Product,'id'>;
export interface User { id:string; name:string; email:string; role:'Admin'|'Customer'; active:boolean; }
export interface OrderLine { productId:string; name:string; unitPrice:number; quantity:number; }
export interface Shipping { name:string; line1:string; city:string; postalCode:string; country:string; }
export interface Order { id:string; userId:string; lines:OrderLine[]; shipping:Shipping; total:number; paymentStatus:string; fulfillmentStatus:string; createdAt:string; checkoutUrl:string|null; }
export interface Checkout { items:{productId:string;quantity:number}[]; shipping:Shipping; }
export interface CartItem { product:Product; quantity:number; }
