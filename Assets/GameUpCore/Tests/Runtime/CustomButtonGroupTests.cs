using System.Collections.Generic;
using System.Reflection;
using GameUp.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace GameUp.Core.Tests
{
    /// <summary>
    /// PlayMode test: listener được gắn/gỡ theo vòng đời OnEnable/OnDisable.
    /// </summary>
    public class CustomButtonGroupTests
    {
        private GameObject _root;
        private CustomButtonGroup _group;
        private List<CustomSelectButton> _buttons;
        private int _dispatchCount;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ButtonGroupTest");
            _root.SetActive(false);
            _group = _root.AddComponent<CustomButtonGroup>();
            _buttons = new List<CustomSelectButton> { CreateButton("A"), CreateButton("B") };
            AssignButtons(_buttons);

            _dispatchCount = 0;
            _group.AddListener(CountDispatch);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Click_WhileEnabled_SelectsButtonAndDispatchesOnce()
        {
            _root.SetActive(true);

            _buttons[1].onClick.Invoke();

            Assert.AreEqual(1, _dispatchCount);
            Assert.IsTrue(_buttons[1].IsSelect);
            Assert.IsFalse(_buttons[0].IsSelect);
        }

        [Test]
        public void Click_AfterDisable_IsIgnored()
        {
            _root.SetActive(true);
            _root.SetActive(false);

            _buttons[1].onClick.Invoke();

            Assert.AreEqual(0, _dispatchCount, "OnDisable phải gỡ được listener đã gắn ở OnEnable.");
            Assert.IsFalse(_buttons[1].IsSelect);
        }

        [Test]
        public void Click_AfterEnableCycles_StillWorks()
        {
            _root.SetActive(true);
            _root.SetActive(false);
            _root.SetActive(true);

            _buttons[0].onClick.Invoke();

            Assert.AreEqual(1, _dispatchCount);
            Assert.IsTrue(_buttons[0].IsSelect);
        }

        [Test]
        public void Disable_KeepsListenersAddedByOtherCode()
        {
            var externalCount = 0;
            _root.SetActive(true);
            _buttons[0].onClick.AddListener(() => externalCount++);
            _root.SetActive(false);

            _buttons[0].onClick.Invoke();

            Assert.AreEqual(1, externalCount);
        }

        [Test]
        public void EnableCycle_WithNullEntry_DoesNotThrow()
        {
            AssignButtons(new List<CustomSelectButton> { _buttons[0], null });

            Assert.DoesNotThrow(() =>
            {
                _root.SetActive(true);
                _root.SetActive(false);
            });
        }

        private CustomSelectButton CreateButton(string buttonName)
        {
            var buttonObject = new GameObject(buttonName);
            buttonObject.transform.SetParent(_root.transform);
            var button = buttonObject.AddComponent<CustomSelectButton>();
            button.onClick = new UnityEvent();
            return button;
        }

        private void AssignButtons(List<CustomSelectButton> buttons)
        {
            typeof(CustomButtonGroup)
                .GetField("buttons", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_group, buttons);
        }

        private void CountDispatch(int index)
        {
            _dispatchCount++;
        }
    }
}
