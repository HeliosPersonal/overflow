namespace Overflow.DataSeederService.Templates;

public static class AnswerTemplates
{
    private static readonly List<string> GenericAnswers = new()
    {
        "Great question! Here's how I would approach this:\n\nThe key is to understand the underlying concept first. Let me break it down:\n\n1. Start by identifying the core problem\n2. Look for existing solutions or patterns\n3. Implement a basic version first\n4. Then optimize as needed\n\nHere's a simple example:\n```\n// Your code here\n```\n\nThis should solve your immediate problem. Let me know if you need more clarification!",
        
        "I've encountered this exact issue before. The solution is actually simpler than you might think.\n\nThe problem is related to how the underlying system handles this particular case. Here's what you need to do:\n\n```\n// Implementation\n```\n\nThis approach has worked well for me in production. Make sure to also consider edge cases like null values and error handling.",
        
        "There are a few ways to solve this, but I recommend this approach:\n\nFirst, make sure you understand the requirements clearly. Then:\n\n- Step 1: Set up the basic structure\n- Step 2: Implement the core logic\n- Step 3: Add error handling\n- Step 4: Test thoroughly\n\nHere's a working example:\n```\n// Code example\n```\n\nThis is considered a best practice in the industry.",
        
        "I think the confusion here comes from a common misconception. Let me clarify:\n\nThe difference between these two approaches is important:\n- Option A is better for small-scale use\n- Option B is more scalable for production\n\nIn your case, I would recommend Option B because it's more maintainable.\n\nHope this helps!",
        
        "This is a well-known issue in the community. The recommended solution is:\n\n1. Update your configuration\n2. Make sure dependencies are up to date\n3. Use this pattern instead:\n\n```\n// Better approach\n```\n\nYou might also want to check the official documentation for more details on this topic.",
        
        "You're on the right track, but there's a more efficient way to do this.\n\nThe performance issue you're seeing is likely due to the way this is being processed. Try this optimization:\n\n```\n// Optimized version\n```\n\nThis should give you much better performance, especially with larger datasets.",
        
        "I had the same problem last week! After some research, I found that the issue is caused by a common pitfall.\n\nHere's the fix:\n```\n// Solution\n```\n\nAlso, make sure to add proper error handling and logging so you can debug issues more easily in the future.",
        
        "The answer depends on your specific use case, but generally:\n\n**Pros:**\n- Easy to implement\n- Well documented\n- Good community support\n\n**Cons:**\n- Might have performance overhead\n- Requires additional dependencies\n\nFor your situation, I would suggest going with the first approach unless you have specific scalability requirements."
    };

    public static string GetRandomAnswer()
    {
        return GenericAnswers[Random.Shared.Next(GenericAnswers.Count)];
    }
}
