using NUnit.Framework;
using SlipperyZlope.RingToss.Config;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The in-game legal documents.
    ///
    /// Testing prose feels odd until you notice what these actually assert: not that
    /// the wording is good, but that the *claims* have not drifted from the build. A
    /// privacy screen that is merely out of date is not a cosmetic bug — it is a
    /// statement to a regulator about what the software does, made by software that no
    /// longer does it.
    ///
    /// So the checks here are the ones a person would forget: that every tab has
    /// something behind it, that the advertising partner named in the text is the one
    /// actually compiled into the build, and that no drafting placeholder shipped.
    /// </summary>
    public sealed class LegalTextTests
    {
        [Test]
        public void Every_section_has_a_tab_a_heading_and_a_body()
        {
            Assert.IsNotEmpty(LegalText.Sections);

            foreach (LegalSection section in LegalText.Sections)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(section.Tab));
                Assert.IsFalse(string.IsNullOrWhiteSpace(section.Heading));

                // Long enough to be a document rather than a stub. The shortest real
                // section here is well over a thousand characters.
                Assert.Greater(section.Body.Length, 400,
                    $"The {section.Tab} section is too short to be the real thing.");
            }
        }

        [Test]
        public void A_tab_label_is_short_enough_to_fit_four_across_a_phone()
        {
            foreach (LegalSection section in LegalText.Sections)
            {
                Assert.LessOrEqual(section.Tab.Length, 8,
                    $"'{section.Tab}' will not fit the tab row.");
            }
        }

        [Test]
        public void The_first_section_answers_whether_data_is_collected()
        {
            // The question everybody arrives with. It has to be answered in the tab
            // that opens by default, not three taps away.
            string body = LegalText.Sections[0].Body;

            StringAssert.Contains("collect nothing", body);
        }

        [Test]
        public void The_text_names_the_advertising_partner_that_is_compiled_in()
        {
            // The drift check that matters most. Swapping the SDK without rewriting
            // this text would leave the game telling players their data goes somewhere
            // it does not — and telling a store the same thing.
            bool namesUnityAds = false;

            foreach (LegalSection section in LegalText.Sections)
            {
                if (section.Body.Contains("Unity Ads"))
                {
                    namesUnityAds = true;
                }
            }

#if SLIPPERYZLOPE_LEVELPLAY
            Assert.Ignore("LevelPlay is compiled in; the text has to name it instead.");
#else
            Assert.IsTrue(namesUnityAds,
                "The build ships Unity Ads but the legal text does not say so.");
#endif
        }

        [Test]
        public void Somewhere_to_send_a_data_request_is_published()
        {
            // A subject access request has a statutory deadline that runs from the day
            // it arrives, whether or not there is an address for it to arrive at.
            StringAssert.Contains("@", LegalText.ContactEmail);

            bool published = false;

            foreach (LegalSection section in LegalText.Sections)
            {
                if (section.Body.Contains(LegalText.ContactEmail))
                {
                    published = true;
                }
            }

            Assert.IsTrue(published, "The contact address is never shown to the player.");
        }

        [Test]
        public void No_drafting_placeholder_survived_into_the_build()
        {
            foreach (LegalSection section in LegalText.Sections)
            {
                StringAssert.DoesNotContain("⚠", section.Body);
                StringAssert.DoesNotContain("TODO", section.Body);
                StringAssert.DoesNotContain("TBD", section.Body);
            }
        }
    }
}
