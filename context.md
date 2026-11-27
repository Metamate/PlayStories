My wife and I are working on an app for our children. The purpose is an "interactive story book" that our kids can sit with on a tablet/ipad and click through "interactive pages" and stories. For starters, we want to make some "books" in the app that targets our 7 month old daughter which just contains some basic interactivity like clicking on an animal and it makes a sound and small animation. Later on we would like to make some books that have a bit more interactivity and story to it to fit our 2½ year old better.

In the "RawAssets" folder you can find assets and sketches that my wife has drawn to give you an idea of the app structure and layout, and some of the assets for our first book (farm animals).

I want you to implement a working prototype for the app, with the welcome screen, book selection screen and the "book screen", everything with working ui. You can use the assets my wife has created and create some placeholder art in the areas where you have no final assets yet. Right now, we only have one image per animal, but we would like them to have some cute animations as well. Help us decide on the best way to implement the animation system so that we get maximum effeciency and dont have to author every animation as a bespoke sprite (or maybe you can help us generate some of the missing sprites to bring the animals to life?). Either way, animations are an important aspect of the app, and we want them to look good and polished.

We also want voice over narrating, for the first book it would just be the name of each animal. There should also be some cute animal sounds when you click them together with the animation. All the animals for the first book should be in the farm setting my wife has created. You can maybe find some fitting farm ambience to make it feel a bit more cozy.

As you can see in the sketches, for the first book you simply "turn the page" to the next animal. There should probably be some nice transition here so it's "fun" to turn the page. Then once you are on a page with an animal, the narrator calls our it's name and you can click the animal which performs a small animation and says an iconic sound for the animal. There should also be a sound button as you can see in the sketch that you can click so that the animals name is read out loud again.

As the app needs to run on different handheld devices, it is important that you make sure that the UI is scalable to various screen aspect ratios that is commonly found on phones and tablets.

Please implement the app in this unity project to the best of your ability, based on the above information and the assets in the RawAssets folder. Let me know if you have any questions for clarification.

---

The app is in danish, but needs to suppport internationalisation. For now, danish and english language support is enough. Please correct anything based on your previous assumption of norwegian language, and please use this elevenlabs API key for better quality narrator voices: sk_282b8818afe08f3ede6ce18463fe8c5a5126c06f9265809f
